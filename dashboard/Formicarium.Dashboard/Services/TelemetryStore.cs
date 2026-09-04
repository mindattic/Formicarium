using Formicarium.Dashboard.Models;
using Microsoft.Data.Sqlite;

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
public sealed class TelemetryStore
{
    private readonly string _connectionString;
    private readonly ILogger<TelemetryStore> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private long[]? _previousCumulative;

    public TelemetryStore(IConfiguration configuration, ILogger<TelemetryStore> logger)
    {
        _logger = logger;

        var path = configuration["Telemetry:DatabasePath"] ?? "formicarium.db";
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        Initialise();
    }

    private void Initialise()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        // Failed sensors are stored as NULL rather than as a value plus a flag. On the wire the
        // firmware uses explicit Ok flags because its JSON serialiser is minimal; in a database
        // NULL is the honest representation, and it makes AVG and MIN skip bad samples for free
        // instead of quietly averaging in zeroes.
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS samples (
                ts             INTEGER PRIMARY KEY,
                nest_bottom_c  REAL,
                nest_top_c     REAL,
                outworld_c     REAL,
                nest_rh        REAL,
                outworld_rh    REAL,
                soil_pct       REAL,
                heater         INTEGER NOT NULL,
                refill         INTEGER NOT NULL,
                feed           INTEGER NOT NULL,
                fan            INTEGER NOT NULL,
                faults         INTEGER NOT NULL,
                lighting_mode  INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS riser_samples (
                ts          INTEGER NOT NULL,
                riser       INTEGER NOT NULL,
                cumulative  INTEGER NOT NULL,
                crossings   INTEGER NOT NULL,
                rate        REAL    NOT NULL,
                channel     INTEGER NOT NULL,
                brightness  REAL    NOT NULL,
                day_number  INTEGER NOT NULL,
                PRIMARY KEY (ts, riser)
            );

            CREATE INDEX IF NOT EXISTS ix_riser_channel ON riser_samples (channel);
            CREATE INDEX IF NOT EXISTS ix_riser_day ON riser_samples (day_number, riser);
            """;

        command.ExecuteNonQuery();
    }

    public async Task RecordAsync(DeviceState state, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT OR REPLACE INTO samples
                        (ts, nest_bottom_c, nest_top_c, outworld_c, nest_rh, outworld_rh, soil_pct,
                         heater, refill, feed, fan, faults, lighting_mode)
                    VALUES ($ts, $nb, $nt, $ow, $nrh, $orh, $soil, $h, $m, $f, $fan, $faults, $mode);
                    """;

                command.Parameters.AddWithValue("$ts", state.TimestampUnixMs);
                command.Parameters.AddWithValue("$nb", Nullable(state.NestBottomTempC, state.NestBottomTempOk));
                command.Parameters.AddWithValue("$nt", Nullable(state.NestTopTempC, state.NestTopTempOk));
                command.Parameters.AddWithValue("$ow", Nullable(state.OutworldTempC, state.OutworldTempOk));
                command.Parameters.AddWithValue("$nrh", Nullable(state.NestHumidityPct, state.NestHumidityOk));
                command.Parameters.AddWithValue("$orh", Nullable(state.OutworldHumidityPct, state.OutworldHumidityOk));
                command.Parameters.AddWithValue("$soil", Nullable(state.SoilMoisturePct, state.SoilMoistureOk));
                command.Parameters.AddWithValue("$h", state.HeaterOn ? 1 : 0);
                command.Parameters.AddWithValue("$m", state.RefillPumpOn ? 1 : 0);
                command.Parameters.AddWithValue("$f", state.FeedPumpOn ? 1 : 0);
                command.Parameters.AddWithValue("$fan", state.FanOn ? 1 : 0);
                command.Parameters.AddWithValue("$faults", state.Faults);
                command.Parameters.AddWithValue("$mode", state.LightingMode);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

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

                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT OR REPLACE INTO riser_samples
                        (ts, riser, cumulative, crossings, rate, channel, brightness, day_number)
                    VALUES ($ts, $riser, $cum, $cross, $rate, $channel, $bright, $day);
                    """;

                command.Parameters.AddWithValue("$ts", state.TimestampUnixMs);
                command.Parameters.AddWithValue("$riser", riser);
                command.Parameters.AddWithValue("$cum", cumulative);
                command.Parameters.AddWithValue("$cross", crossings);
                command.Parameters.AddWithValue("$rate", At(state.RiserRatesPerMinute, riser));
                command.Parameters.AddWithValue("$channel", (int)At(state.RiserChannel, riser));
                command.Parameters.AddWithValue("$bright", At(state.RiserBrightness, riser));
                command.Parameters.AddWithValue("$day", state.AssignmentDayNumber);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            _previousCumulative = (long[])state.RiserCounts.Clone();
        }
        catch (Exception ex)
        {
            // Losing a telemetry row must never take down the dashboard, and certainly must not
            // interfere with the controller keeping the colony alive.
            _logger.LogWarning(ex, "Failed to record telemetry sample");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<TrendPoint>> GetTrendAsync(TimeSpan window, int maxPoints = 400, CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.Subtract(window).ToUnixTimeMilliseconds();
        var points = new List<TrendPoint>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ts, nest_bottom_c, nest_top_c, outworld_c, soil_pct, heater
            FROM samples
            WHERE ts >= $since
            ORDER BY ts;
            """;
        command.Parameters.AddWithValue("$since", since);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            points.Add(new TrendPoint(
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)),
                reader.IsDBNull(1) ? null : reader.GetDouble(1),
                reader.IsDBNull(2) ? null : reader.GetDouble(2),
                reader.IsDBNull(3) ? null : reader.GetDouble(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.GetInt32(5) == 1));
        }

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
        var results = new List<ChannelTotal>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT channel, SUM(crossings), COUNT(DISTINCT day_number || ':' || riser)
            FROM riser_samples
            GROUP BY channel
            ORDER BY channel;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ChannelTotal(reader.GetInt32(0), reader.GetInt64(1), reader.GetInt32(2)));
        }

        return results;
    }

    /// <summary>The same crossings grouped by tube, which is the positional confound the rotation controls for.</summary>
    public async Task<IReadOnlyList<RiserDayTotal>> GetRiserTotalsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<RiserDayTotal>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT riser, channel, SUM(crossings)
            FROM riser_samples
            GROUP BY riser, channel
            ORDER BY riser, channel;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new RiserDayTotal(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt64(2)));
        }

        return results;
    }

    private static object Nullable(double value, bool ok) => ok ? value : DBNull.Value;

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
