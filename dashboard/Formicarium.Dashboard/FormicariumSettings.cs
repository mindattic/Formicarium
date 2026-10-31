using Formicarium.Dashboard.Services;
using MindAttic.Vault.Settings;
using MindAttic.Vault.Settings.Manifest;

namespace Formicarium.Dashboard;

/// <summary>
/// The dashboard's settings in <c>%APPDATA%\MindAttic\Formicarium\settings.json</c>, edited in
/// MindAttic.Vault.Desktop. Imported once from the old <c>appsettings.json</c> keys; read at startup.
/// </summary>
[SettingsApp(AppId, "Formicarium", Icon = "bug", Description = "Ant colony dashboard: climate control, telemetry and camera.")]
public sealed class FormicariumSettings
{
    public const string AppId = "Formicarium";

    [Setting("Controller", Order = 1)]
    public ControllerSettings Controller { get; set; } = new();

    [Setting("Camera", Order = 2)]
    public CameraSettings Camera { get; set; } = new();

    [Setting("Database", Group = "Storage", Order = 3,
        Help = "SQL Server connection string for telemetry and build progress.", NotSecret = true)]
    public string DatabaseConnection { get; set; } =
        @"Server=(localdb)\MSSQLLocalDB;Database=Formicarium;Trusted_Connection=True;TrustServerCertificate=True";

    public sealed class ControllerSettings
    {
        [Setting("Use the simulator", Group = "Controller", Order = 1,
            Help = "On: a simulated colony, no hardware needed. Off: talk to the ESP32 at the address below.")]
        public bool UseSimulator { get; set; } = true;

        [Setting("ESP32 address", Group = "Controller", Order = 2,
            Help = "Base URL of the controller on your network, used when the simulator is off.")]
        [UrlSetting]
        public string BaseAddress { get; set; } = "http://formicarium.local";
    }

    public sealed class CameraSettings
    {
        [Setting("Stream URL", Group = "Camera", Order = 1,
            Help = "MJPEG or snapshot URL of the IR-capable IP camera. Empty hides the camera page content.")]
        public string StreamUrl { get; set; } = "";
    }

    /// <summary>
    /// Loads the Vault settings. On the first run after LAMP, values in the old <c>appsettings.json</c>
    /// keys (Controller:*, Camera:StreamUrl, ConnectionStrings:Formicarium) are imported so nothing changes.
    /// </summary>
    public static FormicariumSettings Load(IConfiguration? legacy = null, ISettingsStore<FormicariumSettings>? store = null)
    {
        store ??= JsonSettingsStore<FormicariumSettings>.ForSettingsApp();
        if (!store.Exists() && legacy is not null)
        {
            var imported = new FormicariumSettings();
            imported.Controller.UseSimulator = legacy.GetValue("Controller:UseSimulator", imported.Controller.UseSimulator);
            imported.Controller.BaseAddress = legacy["Controller:BaseAddress"] is { Length: > 0 } b ? b : imported.Controller.BaseAddress;
            imported.Camera.StreamUrl = legacy["Camera:StreamUrl"] ?? imported.Camera.StreamUrl;
            imported.DatabaseConnection = legacy.GetConnectionString("Formicarium") is { Length: > 0 } c ? c : imported.DatabaseConnection;
            store.Save(imported);
        }
        return store.LoadOrSeed();
    }

    /// <summary>Registers the controller client the settings choose: the simulator, or HTTP to the ESP32.</summary>
    public void AddControllerClient(IServiceCollection services)
    {
        if (Controller.UseSimulator)
        {
            services.AddSingleton<IFormicariumClient, FakeFormicariumClient>();
            return;
        }
        if (string.IsNullOrWhiteSpace(Controller.BaseAddress))
            throw new InvalidOperationException("Set the ESP32 address in MindAttic Vault (Formicarium tab) or turn the simulator on.");
        services.AddHttpClient<IFormicariumClient, HttpFormicariumClient>(http =>
        {
            http.BaseAddress = new Uri(Controller.BaseAddress);
            // Short, because the control loop matters more than this request does. A wedged HTTP
            // call must not pile up behind the poller.
            http.Timeout = TimeSpan.FromSeconds(4);
        });
    }
}
