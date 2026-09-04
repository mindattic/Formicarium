using System.Text.Json;
using Formicarium.Dashboard.Models;

namespace Formicarium.Dashboard.Services;

/// <summary>
/// Talks to the ESP32's HTTP API.
/// </summary>
public sealed class HttpFormicariumClient(HttpClient http, ILogger<HttpFormicariumClient> logger)
    : IFormicariumClient
{
    // The device serialises with nanoFramework.Json, which emits the CLR property names
    // verbatim in PascalCase. Case-insensitive matching means this keeps working if the device
    // serialiser is ever swapped for one that camel-cases.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsSimulated => false;

    public async Task<DeviceState?> GetStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync("/api/state", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // 503 is the device saying it is up but has not completed a tick yet, which is
                // normal for the first second after a reset and not worth an error.
                logger.LogDebug("Device returned {Status} for /api/state", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<DeviceState>(json, Options);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // An unreachable controller is an expected condition, not an exceptional one: the
            // column keeps the colony alive without the network, so the dashboard treats this
            // as "no data" and says so rather than throwing.
            logger.LogDebug(ex, "Controller unreachable");
            return null;
        }
    }

    public Task SetLightingModeAsync(LightingMode mode, CancellationToken cancellationToken = default)
        => PostAsync($"/api/lighting?mode={(int)mode}", cancellationToken);

    public async Task<bool> RequestFeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.PostAsync("/api/feed", null, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // The device reports refusal (rate limit, service mode) in the body rather than as
            // an HTTP error, because a refused dose is a normal outcome.
            return body.Contains("\"ok\":true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Feed request failed");
            return false;
        }
    }

    public Task SetServiceModeAsync(bool enabled, CancellationToken cancellationToken = default)
        => PostAsync($"/api/service?enabled={(enabled ? 1 : 0)}", cancellationToken);

    public Task AcknowledgeHydrationAsync(CancellationToken cancellationToken = default)
        => PostAsync("/api/hydration/ack", cancellationToken);

    private async Task PostAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsync(path, null, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Command {Path} failed", path);
        }
    }
}
