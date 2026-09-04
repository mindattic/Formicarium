using Formicarium.Dashboard.Components;
using Formicarium.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
