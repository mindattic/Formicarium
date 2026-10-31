using Formicarium.Dashboard.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MindAttic.Vault.Launch;
using MindAttic.Vault.Paths;
using MindAttic.Vault.Settings;
using MindAttic.Vault.Settings.Manifest;
using NUnit.Framework;

namespace Formicarium.Dashboard.Tests;

/// <summary>Every test in this assembly sees temp Vault roots, so nothing can write the real AppData.</summary>
[SetUpFixture]
public sealed class TestVaultRoots
{
    private string root = "";

    [OneTimeSetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "FormicariumVault-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(VaultPaths.RoamingRootEnvVar, Path.Combine(root, "roaming"));
        Environment.SetEnvironmentVariable(VaultPaths.LocalRootEnvVar, Path.Combine(root, "local"));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(VaultPaths.RoamingRootEnvVar, null);
        Environment.SetEnvironmentVariable(VaultPaths.LocalRootEnvVar, null);
        try { Directory.Delete(root, recursive: true); } catch { }
    }
}

/// <summary>LAMP (FOR-T23): the dashboard's settings in Vault, one end-to-end test per setting.</summary>
[NonParallelizable]
public class LampSettingsTests
{
    private static string Folder => VaultPaths.RoamingBucket(FormicariumSettings.AppId);

    [SetUp]
    public void SetUp()
    {
        if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
    }

    private static IConfiguration Legacy(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceProvider Controllers(FormicariumSettings s)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        s.AddControllerClient(services);
        return services.BuildServiceProvider();
    }

    [Test]
    public void Setting_UseSimulator_ImportsRoundTripsAndPicksTheClient()
    {
        var s = FormicariumSettings.Load(Legacy(new() { ["Controller:UseSimulator"] = "false", ["Controller:BaseAddress"] = "http://10.0.0.9" }));
        Assert.That(s.Controller.UseSimulator, Is.False, "old appsettings value imported");
        Assert.That(FormicariumSettings.Load().Controller.UseSimulator, Is.False, "persisted in Vault");
        using (var sp = Controllers(s))
            Assert.That(sp.GetRequiredService<IFormicariumClient>(), Is.InstanceOf<HttpFormicariumClient>());

        JsonSettingsStore<FormicariumSettings>.ForSettingsApp().Update(x => x.Controller.UseSimulator = true);
        using (var sp = Controllers(FormicariumSettings.Load()))
            Assert.That(sp.GetRequiredService<IFormicariumClient>(), Is.InstanceOf<FakeFormicariumClient>());
    }

    [Test]
    public void Setting_BaseAddress_ImportsRoundTripsAndIsTheControllerUrl()
    {
        var s = FormicariumSettings.Load(Legacy(new() { ["Controller:UseSimulator"] = "false", ["Controller:BaseAddress"] = "http://10.0.0.9" }));
        Assert.That(s.Controller.BaseAddress, Is.EqualTo("http://10.0.0.9"));
        JsonSettingsStore<FormicariumSettings>.ForSettingsApp().Update(x => x.Controller.BaseAddress = "http://ants.lan:8080");
        using var sp = Controllers(FormicariumSettings.Load());
        var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(IFormicariumClient).Name);
        Assert.That(http.BaseAddress, Is.EqualTo(new Uri("http://ants.lan:8080")));
        Assert.That(http.Timeout, Is.EqualTo(TimeSpan.FromSeconds(4)));
    }

    [Test]
    public void Setting_StreamUrl_ImportsRoundTripsAndIsWhatTheCameraPageShows()
    {
        Assert.That(FormicariumSettings.Load(Legacy(new() { ["Camera:StreamUrl"] = "http://cam/mjpeg" })).Camera.StreamUrl, Is.EqualTo("http://cam/mjpeg"));
        JsonSettingsStore<FormicariumSettings>.ForSettingsApp().Update(x => x.Camera.StreamUrl = "http://cam2/snap.jpg");
        var services = new ServiceCollection();
        services.AddSingleton(FormicariumSettings.Load());
        using var sp = services.BuildServiceProvider();
        Assert.That(sp.GetRequiredService<FormicariumSettings>().Camera.StreamUrl, Is.EqualTo("http://cam2/snap.jpg"), "the Camera page injects these settings");
        Assert.That(SettingsManifestBuilder.Build<FormicariumSettings>().Find(Components.Pages.Camera.StreamUrlSetting), Is.Not.Null,
            "the page's deep link names a real setting");
    }

    [Test]
    public void Setting_DatabaseConnection_ImportsRoundTripsAndIsTheDatabaseUsed()
    {
        const string cs = "Server=.;Database=Ants;Trusted_Connection=True";
        Assert.That(FormicariumSettings.Load(Legacy(new() { ["ConnectionStrings:Formicarium"] = cs })).DatabaseConnection, Is.EqualTo(cs));
        Assert.That(FormicariumSettings.Load().DatabaseConnection, Is.EqualTo(cs), "Program.cs opens this connection");
    }

    [Test]
    public void EverySettingHasItsOwnTest()
    {
        var tested = GetType().GetMethods().Select(m => m.Name).Where(n => n.StartsWith("Setting_")).ToList();
        foreach (var field in SettingsManifestBuilder.Build<FormicariumSettings>().AllFields().Where(f => f.Kind != SettingKind.Object))
            Assert.That(tested.Any(t => t.StartsWith("Setting_" + char.ToUpperInvariant(field.Property[0]) + field.Property[1..] + "_")), Is.True, field.Property);
    }

    [Test]
    public void FreshMachine_SeedsTodaysDefaultsAndTheManifest()
    {
        var s = FormicariumSettings.Load(Legacy(new()));
        Assert.That((s.Controller.UseSimulator, s.Controller.BaseAddress, s.Camera.StreamUrl), Is.EqualTo((true, "http://formicarium.local", "")));
        Assert.That(s.DatabaseConnection, Does.Contain("MSSQLLocalDB"));
        var m = SettingsManifestFile.TryRead(Path.Combine(Folder, SettingsManifest.FileName))!;
        Assert.That(m.AllFields().Where(f => f.Kind != SettingKind.Object).All(f => !string.IsNullOrWhiteSpace(f.Help)), Is.True);
    }

    [Test]
    public void OldAppSettingsKeysAreGone()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Formicarium.Dashboard", "appsettings.json");
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Not.Contain("Controller").And.Not.Contain("Camera").And.Not.Contain("ConnectionStrings"));
    }

    [Test]
    public void CameraLink_OpensVaultAtTheStreamUrlSetting()
    {
        var link = VaultSettings.Link(FormicariumSettings.AppId, Components.Pages.Camera.StreamUrlSetting);
        Assert.That(VaultOpenRequest.TryParseUrl(link, out var r), Is.True);
        Assert.That((r!.AppId, r.Setting), Is.EqualTo(("Formicarium", "MindAttic:Settings:Formicarium:camera:streamUrl")));
    }
}
