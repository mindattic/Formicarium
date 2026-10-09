using Formicarium.Dashboard.Components;
using Formicarium.Dashboard.Data;
using Formicarium.Dashboard.Services;
using Microsoft.EntityFrameworkCore;
using MindAttic.Log;
using MindAttic.Log.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("Formicarium")
                        ?? throw new InvalidOperationException("ConnectionStrings:Formicarium must be set.");

builder.Services.AddDbContextFactory<FormicariumDbContext>(options => options.UseSqlServer(connectionString));

// First MindAttic.Log integration for this app (see that repo's docs/MIGRATION.md): a genuine
// general-purpose SQL Server database of its own (Samples/RiserSamples/BuildProgress), not an
// auth-only one, so this is the SQL Server tier. ColonyMonitor/TelemetryStore already call
// ILogger<T> — no call-site changes, just the sink underneath. The table itself is created once
// at startup below, next to the existing EF migration; AddMindAtticLog never auto-creates it.
builder.Services.AddMindAtticLog(o =>
{
    o.Application = "Formicarium";
    o.Destination = LogDestination.SqlServer;
    o.SqlServerConnectionString = connectionString;
});

builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<BuildProgressStore>();

// Simulator by default, because nothing has been ordered yet and a dashboard that only works
// once hardware exists cannot be developed against. Point Controller:BaseAddress at the ESP32
// and set Controller:UseSimulator to false to switch over; nothing else changes.
var useSimulator = builder.Configuration.GetValue("Controller:UseSimulator", true);

if (useSimulator)
{
    builder.Services.AddSingleton<IFormicariumClient, FakeFormicariumClient>();
}
else
{
    var baseAddress = builder.Configuration["Controller:BaseAddress"]
                      ?? throw new InvalidOperationException(
                          "Controller:BaseAddress must be set when Controller:UseSimulator is false.");

    builder.Services.AddHttpClient<IFormicariumClient, HttpFormicariumClient>(http =>
    {
        http.BaseAddress = new Uri(baseAddress);

        // Short, because the control loop matters more than this request does. A wedged HTTP
        // call must not pile up behind the poller.
        http.Timeout = TimeSpan.FromSeconds(4);
    });
}

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

    // MindAttic.Log's table is not EF-managed and never auto-created by the sink (LOG-LAW-1) —
    // created here, once, next to the DbContext's own migration above.
    await using var logSchemaConnection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
    await logSchemaConnection.OpenAsync();
    await using var logSchemaCommand = logSchemaConnection.CreateCommand();
    logSchemaCommand.CommandText = MindAttic.Log.Schema.LogSchema.CreateTableSqlServer;
    await logSchemaCommand.ExecuteNonQueryAsync();
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
