using Formicarium.Dashboard.Data;
using Formicarium.Dashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace Formicarium.Dashboard.Services;

public sealed record TrendPoint(DateTimeOffset At, double? NestBottomC, double? NestTopC, double? OutworldC, double? SoilPct, bool HeaterOn);

public sealed record RiserDayTotal(int Riser, int Channel, long Crossings);

public sealed record ChannelTotal(int Channel, long Crossings, int RiserDays);

/// <summary>
/// The persistent log.
///
/// Two things are stored that the firmware cannot keep for itself. The obvious one is history:
/// the ESP32 has no room for it and loses everything on reset. The less obvious and more
/// important one is the <b>experimental covariates</b> — every traffic row carries the channel
/// that riser was showing and how bright it was at the time. A count without the stimulus that
/// produced it is uninterpretable after the fact, and by then the mapping has rotated away.
/// </summary>
public sealed class TelemetryStore(IDbContextFactory<FormicariumDbContext> contextFactory, ILogger<TelemetryStore> logger)
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private long[]? _previousCumulative;

    public async Task RecordAsync(DeviceState state, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var existingSample = await context.Samples.FindAsync([state.TimestampUnixMs], cancellationToken);
            if (existingSample is not null)
            {
                context.Samples.Remove(existingSample);
            }

            context.Samples.Add(new Sample
            {
                Ts = state.TimestampUnixMs,
                NestBottomC = Nullable(state.NestBottomTempC, state.NestBottomTempOk),
                NestTopC = Nullable(state.NestTopTempC, state.NestTopTempOk),
                OutworldC = Nullable(state.OutworldTempC, state.OutworldTempOk),
                NestRh = Nullable(state.NestHumidityPct, state.NestHumidityOk),
                OutworldRh = Nullable(state.OutworldHumidityPct, state.OutworldHumidityOk),
                SoilPct = Nullable(state.SoilMoisturePct, state.SoilMoistureOk),
                Heater = state.HeaterOn,
                Refill = state.RefillPumpOn,
                Feed = state.FeedPumpOn,
                Fan = state.FanOn,
                NestFan = state.NestFanOn,
                Faults = state.Faults,
                LightingMode = state.LightingMode
            });

            for (int riser = 0; riser < state.RiserCounts.Length; riser++)
            {
                long cumulative = state.RiserCounts[riser];
                long crossings = 0;

                if (_previousCumulative is not null && riser < _previousCumulative.Length)
                {
                    // A controller reset restarts the cumulative count at zero, which would
                    // otherwise show up as a large negative delta and corrupt every total that
                    // followed. Treat a decrease as a restart and count from zero.
                    crossings = cumulative >= _previousCumulative[riser]
                        ? cumulative - _previousCumulative[riser]
                        : cumulative;
                }

                var existingRiserSample = await context.RiserSamples.FindAsync([state.TimestampUnixMs, riser], cancellationToken);
                if (existingRiserSample is not null)
                {
                    context.RiserSamples.Remove(existingRiserSample);
                }

                context.RiserSamples.Add(new RiserSample
                {
                    Ts = state.TimestampUnixMs,
                    Riser = riser,
                    Cumulative = cumulative,
                    Crossings = crossings,
                    Rate = At(state.RiserRatesPerMinute, riser),
                    Channel = (int)At(state.RiserChannel, riser),
                    Brightness = At(state.RiserBrightness, riser),
                    DayNumber = state.AssignmentDayNumber
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _previousCumulative = (long[])state.RiserCounts.Clone();
        }
        catch (Exception ex)
        {
            // Losing a telemetry row must never take down the dashboard, and certainly must not
            // interfere with the controller keeping the colony alive.
            logger.LogWarning(ex, "Failed to record telemetry sample");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<TrendPoint>> GetTrendAsync(TimeSpan window, int maxPoints = 400, CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.Subtract(window).ToUnixTimeMilliseconds();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var points = await context.Samples
            .Where(s => s.Ts >= since)
            .OrderBy(s => s.Ts)
            .Select(s => new TrendPoint(
                DateTimeOffset.FromUnixTimeMilliseconds(s.Ts),
                s.NestBottomC,
                s.NestTopC,
                s.OutworldC,
                s.SoilPct,
                s.Heater))
            .ToListAsync(cancellationToken);

        return Decimate(points, maxPoints);
    }

    /// <summary>
    /// Crossings grouped by the colour the riser was showing.
    ///
    /// This is the headline result of the experiment, and it is only meaningful because the
    /// firmware rotates the mapping daily: every riser spends equal time on every channel, so a
    /// difference between channels here cannot be explained by one tube simply being better
    /// placed. Compare against <see cref="GetRiserTotalsAsync"/>, which is the same data grouped
    /// the other way — if position dominates there but colour does not differ here, the light is
    /// doing nothing.
    /// </summary>
    public async Task<IReadOnlyList<ChannelTotal>> GetChannelTotalsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // SQL Server has no COUNT(DISTINCT <tuple>), so the distinct (day, riser) pairs per
        // channel are counted client-side rather than folded into the grouped sum below.
        var sums = await context.RiserSamples
            .GroupBy(r => r.Channel)
            .Select(g => new { Channel = g.Key, Crossings = g.Sum(x => x.Crossings) })
            .ToListAsync(cancellationToken);

        var riserDays = await context.RiserSamples
            .Select(r => new { r.Channel, r.DayNumber, r.Riser })
            .Distinct()
            .ToListAsync(cancellationToken);

        var riserDayCounts = riserDays
            .GroupBy(r => r.Channel)
            .ToDictionary(g => g.Key, g => g.Count());

        return sums
            .Select(s => new ChannelTotal(s.Channel, s.Crossings, riserDayCounts.GetValueOrDefault(s.Channel)))
            .OrderBy(c => c.Channel)
            .ToList();
    }

    /// <summary>The same crossings grouped by tube, which is the positional confound the rotation controls for.</summary>
    public async Task<IReadOnlyList<RiserDayTotal>> GetRiserTotalsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var totals = await context.RiserSamples
            .GroupBy(r => new { r.Riser, r.Channel })
            .Select(g => new { g.Key.Riser, g.Key.Channel, Crossings = g.Sum(x => x.Crossings) })
            .ToListAsync(cancellationToken);

        return totals
            .Select(t => new RiserDayTotal(t.Riser, t.Channel, t.Crossings))
            .OrderBy(r => r.Riser).ThenBy(r => r.Channel)
            .ToList();
    }

    private static double? Nullable(double value, bool ok) => ok ? value : null;

    private static double At(double[] values, int index) => index < values.Length ? values[index] : 0.0;

    private static double At(int[] values, int index) => index < values.Length ? values[index] : 0;

    /// <summary>Even sampling down to a plottable number of points, keeping the first and last.</summary>
    private static IReadOnlyList<TrendPoint> Decimate(List<TrendPoint> points, int maxPoints)
    {
        if (points.Count <= maxPoints)
        {
            return points;
        }

        var step = (double)points.Count / maxPoints;
        var result = new List<TrendPoint>(maxPoints);

        for (int i = 0; i < maxPoints; i++)
        {
            result.Add(points[(int)(i * step)]);
        }

        result[^1] = points[^1];
        return result;
    }
}
