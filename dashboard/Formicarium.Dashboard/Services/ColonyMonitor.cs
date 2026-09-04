using Formicarium.Dashboard.Models;

namespace Formicarium.Dashboard.Services;

/// <summary>
/// Polls the column, holds the latest state, and writes the log.
///
/// One poller for the whole application rather than one per open browser tab: the ESP32 serves a
/// single-threaded HTTP stack, and a handful of tabs each polling it once a second would starve
/// the control loop of the CPU it needs to decide whether the nest is warm.
/// </summary>
public sealed class ColonyMonitor(
    IFormicariumClient client,
    TelemetryStore store,
    ILogger<ColonyMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LogInterval = TimeSpan.FromSeconds(30);

    private DateTimeOffset _lastLogged = DateTimeOffset.MinValue;

    /// <summary>Most recent successful poll, or null if the column has never answered.</summary>
    public DeviceState? Latest { get; private set; }

    /// <summary>True once a poll has failed, so the UI can distinguish "no data yet" from "lost contact".</summary>
    public bool Unreachable { get; private set; }

    public DateTimeOffset? LastContact { get; private set; }

    public event Action? Updated;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Colony monitor started against {Source}",
            client.IsSimulated ? "the built-in simulator" : "a real controller");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var state = await client.GetStateAsync(stoppingToken);

                if (state is not null)
                {
                    Latest = state;
                    Unreachable = false;
                    LastContact = DateTimeOffset.UtcNow;

                    if (DateTimeOffset.UtcNow - _lastLogged >= LogInterval)
                    {
                        _lastLogged = DateTimeOffset.UtcNow;
                        await store.RecordAsync(state, stoppingToken);
                    }
                }
                else
                {
                    // Deliberately keeps the last known state on screen rather than blanking it.
                    // Losing the network does not stop the column working, and a stale-but-
                    // labelled reading is more useful to someone deciding whether to intervene
                    // than an empty page.
                    Unreachable = true;
                }

                Updated?.Invoke();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Poll failed");
                Unreachable = true;
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
