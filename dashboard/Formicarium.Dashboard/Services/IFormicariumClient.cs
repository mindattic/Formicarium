using Formicarium.Dashboard.Models;

namespace Formicarium.Dashboard.Services;

/// <summary>
/// Everything the dashboard can ask of the column.
///
/// Two implementations exist: one that talks to the ESP32 over HTTP, and one that simulates a
/// colony. The simulator is not a stub — it models the thermal, moisture and behavioural
/// dynamics well enough that the closed-loop lighting experiment can be watched working before
/// any hardware has been ordered, which is the only way to know the loop is wired the right way
/// round.
/// </summary>
public interface IFormicariumClient
{
    /// <summary>True when this client is a simulation, so the UI can say so plainly.</summary>
    bool IsSimulated { get; }

    Task<DeviceState?> GetStateAsync(CancellationToken cancellationToken = default);

    Task SetLightingModeAsync(LightingMode mode, CancellationToken cancellationToken = default);

    Task<bool> RequestFeedAsync(CancellationToken cancellationToken = default);

    Task SetServiceModeAsync(bool enabled, CancellationToken cancellationToken = default);

    Task AcknowledgeHydrationAsync(CancellationToken cancellationToken = default);
}
