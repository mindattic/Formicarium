using Formicarium.Dashboard;
using Formicarium.Dashboard.Components;
using Formicarium.Dashboard.Data;
using Formicarium.Dashboard.Services;
using Microsoft.EntityFrameworkCore;
using MindAttic.Log.Extensions;
using MindAttic.Vault.Paths;

if (MindAttic.Vault.Settings.Manifest.SettingsManifestCommand.TryRun<FormicariumSettings>(args)) return;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Settings are edited in MindAttic.Vault.Desktop (Formicarium tab); the old appsettings.json keys are imported once.
var settings = FormicariumSettings.Load(builder.Configuration);
builder.Services.AddSingleton(settings);

var connectionString = settings.DatabaseConnection;
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Set the Formicarium database connection in MindAttic Vault (Formicarium tab).");

builder.Services.AddDbContextFactory<FormicariumDbContext>(options => options.UseSqlServer(connectionString));

// Phase 2 (MindAttic.Log 3.0.0, see its docs/BIBLE.md §1.1): logs now live in their own standalone
// MindAttic.Log.db under Vault's shared roaming root, not FormicariumDbContext's SQL Server
// database — Phase 1 wrote dbo.MindAttic_Log into the same database FormicariumDbContext uses;
// that's superseded. ColonyMonitor/TelemetryStore already call ILogger<T> — no call-site changes.
// Phase 1's historical rows are carried forward and that table dropped, below, next to the
// existing EF migration.
builder.Services.AddMindAtticLog(o =>
{
    o.Application = "Formicarium";
    o.DbPath = VaultPaths.LoggingDbPath("Formicarium");
});

builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<BuildProgressStore>();

// Simulator by default, because nothing has been ordered yet and a dashboard that only works
// once hardware exists cannot be developed against. Turn the simulator off and set the ESP32
// address in Vault to switch over; nothing else changes.
settings.AddControllerClient(builder.Services);

builder.Services.AddSingleton<ColonyMonitor>();
builder.Services.AddHostedService(services => services.GetRequiredService<ColonyMonitor>());

var app = builder.Build();

// Applied on every startup rather than via a deploy step: this dashboard has one instance and one
// database, so there is no fleet to coordinate a migration across.
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FormicariumDbContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await context.Database.MigrateAsync();

    // Phase 2: one-time copy of Phase 1's dbo.MindAttic_Log rows into the new standalone database,
    // then drop that table — idempotent and a clean no-op on every later startup, same "applied on
    // every startup, no fleet to coordinate" posture as the EF migration immediately above.
    await MindAttic.Log.Migration.MindAtticLogMigrator.MigrateFromSqlServerAsync(
        connectionString, VaultPaths.LoggingDbPath("Formicarium"), dropSource: true);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// In Development, static files (blueprint.js chief among them) get Cache-Control: no-cache rather
// than the default — which sets no header at all and leaves the browser free to serve a stale copy
// on an ordinary reload without ever asking. no-cache still lets the browser keep the file, but
// forces it to revalidate with the server on every request (a cheap 304 when nothing changed), so
// an edit here shows up on the next plain reload instead of needing an empty-cache hard reload.
var staticFileOptions = app.Environment.IsDevelopment()
    ? new StaticFileOptions { OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache" }
    : new StaticFileOptions();

app.UseStaticFiles(staticFileOptions);
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
