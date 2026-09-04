using Formicarium.Core.Actuation;
using Formicarium.Core.Config;
using Formicarium.Core.Sensing;
using Formicarium.Dashboard.Models;
using CoreState = Formicarium.Core.State.DeviceState;
using ControlLoop = Formicarium.Core.Control.ControlLoop;
using CoreLightingMode = Formicarium.Core.Control.LightingMode;

namespace Formicarium.Dashboard.Services;

/// <summary>
/// A whole column in a process: the real firmware control loop, driven by a simulated colony.
///
/// Simulated time runs faster than real time (see <see cref="SpeedFactor"/>) because everything
/// interesting about this system happens on the scale of hours to days — the mist cooldown, the
/// diurnal foraging cycle, the midnight colour rotation. At wall-clock speed the dashboard would
/// show a flat line and the experiment would take a week to demonstrate.
/// </summary>
public sealed class FakeFormicariumClient : IFormicariumClient
{
    /// <summary>Simulated seconds per real second. 120 puts a full day in twelve minutes.</summary>
    public const double SpeedFactor = 120.0;

    private const double MaxStepSeconds = 1.0;

    private readonly object _gate = new();
    private readonly ColonySimulator _simulator;
    private readonly ControlLoop _loop;
    private readonly SimulatedSensors _sensors;
    private readonly SimulatedActuators _actuators;

    private DateTime _simNowUtc;
    private DateTime _lastRealUtc;
    private CoreState? _latest;

    public FakeFormicariumClient()
    {
        _simulator = new ColonySimulator(PinMap.RiserCount);
        _sensors = new SimulatedSensors(_simulator);
        _actuators = new SimulatedActuators(PinMap.RiserCount);

        // Start mid-morning so the first thing anyone sees is an active colony rather than a
        // dark curfew.
        _simNowUtc = DateTime.UtcNow.Date.AddHours(15);
        _lastRealUtc = DateTime.UtcNow;

        _loop = new ControlLoop(_sensors, _actuators, new Setpoints(), _simNowUtc, PinMap.RiserCount);
    }

    public bool IsSimulated => true;

    public ColonySimulator Simulator => _simulator;

    public DateTime SimulatedTimeUtc
    {
        get { lock (_gate) { return _simNowUtc; } }
    }

    public Task<DeviceState?> GetStateAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            AdvanceLocked();
            return Task.FromResult<DeviceState?>(_latest is null ? null : Map(_latest));
        }
    }

    private void AdvanceLocked()
    {
        var realNow = DateTime.UtcNow;
        double simSeconds = (realNow - _lastRealUtc).TotalSeconds * SpeedFactor;
        _lastRealUtc = realNow;

        // Cap the catch-up so that leaving the tab closed for an hour does not spend minutes
        // grinding through simulated ticks on the next request.
        simSeconds = Math.Min(simSeconds, 6.0 * 3600.0);

        while (simSeconds > 0)
        {
            double step = Math.Min(MaxStepSeconds, simSeconds);
            simSeconds -= step;
            _simNowUtc = _simNowUtc.AddSeconds(step);

            _simulator.Step(
                step,
                _actuators.HeaterOn,
                _actuators.RefillPumpOn,
                _actuators.FanOn,
                _simNowUtc,
                _actuators.Lights);

            // The real firmware policy, unmodified.
            _latest = _loop.Tick(_simNowUtc);
        }
    }

    public Task SetLightingModeAsync(LightingMode mode, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _loop.Lighting.Mode = (CoreLightingMode)(int)mode;
        }

        return Task.CompletedTask;
    }

    public Task<bool> RequestFeedAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_loop.RequestManualFeed(_simNowUtc));
        }
    }

    public Task SetServiceModeAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _loop.ServiceMode = enabled;
        }

        return Task.CompletedTask;
    }

    public Task AcknowledgeHydrationAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _loop.Hydration.Acknowledge();
        }

        return Task.CompletedTask;
    }

    private static DeviceState Map(CoreState source) => new()
    {
        TimestampUnixMs = source.TimestampUnixMs,
        UptimeSeconds = source.UptimeSeconds,
        NestBottomTempC = source.NestBottomTempC,
        NestBottomTempOk = source.NestBottomTempOk,
        NestTopTempC = source.NestTopTempC,
        NestTopTempOk = source.NestTopTempOk,
        OutworldTempC = source.OutworldTempC,
        OutworldTempOk = source.OutworldTempOk,
        NestHumidityPct = source.NestHumidityPct,
        NestHumidityOk = source.NestHumidityOk,
        OutworldHumidityPct = source.OutworldHumidityPct,
        OutworldHumidityOk = source.OutworldHumidityOk,
        SoilMoisturePct = source.SoilMoisturePct,
        SoilMoistureOk = source.SoilMoistureOk,
        ReservoirFull = source.ReservoirFull,
        RiserCounts = (long[])source.RiserCounts.Clone(),
        RiserRatesPerMinute = (double[])source.RiserRatesPerMinute.Clone(),
        RiserDebouncedCounts = (long[])source.RiserDebouncedCounts.Clone(),
        RiserChannel = (int[])source.RiserChannel.Clone(),
        RiserBrightness = (double[])source.RiserBrightness.Clone(),
        AssignmentDayNumber = source.AssignmentDayNumber,
        LightingMode = source.LightingMode,
        LightCurfewActive = source.LightCurfewActive,
        HeaterOn = source.HeaterOn,
        RefillPumpOn = source.RefillPumpOn,
        FeedPumpOn = source.FeedPumpOn,
        FanOn = source.FanOn,
        RefillWanted = source.RefillWanted,
        Faults = source.Faults,
        ServiceMode = source.ServiceMode
    };

    private sealed class SimulatedSensors(ColonySimulator simulator) : ISensorSet
    {
        public SensorSnapshot Poll(DateTime nowUtc) => simulator.BuildSnapshot(nowUtc);
    }

    private sealed class SimulatedActuators(int riserCount) : IActuatorSet
    {
        public bool HeaterOn { get; private set; }
        public bool RefillPumpOn { get; private set; }
        public bool FeedPumpOn { get; private set; }
        public bool FanOn { get; private set; }
        public RgbColor[] Lights { get; private set; } = new RgbColor[riserCount];

        public void SetHeater(bool on) => HeaterOn = on;

        public void SetRefillPump(bool on) => RefillPumpOn = on;

        public void SetFeedPump(bool on) => FeedPumpOn = on;

        public void SetFan(bool on) => FanOn = on;

        public void SetRiserLights(RgbColor[] colors) => Lights = colors;

        public void AllOff()
        {
            HeaterOn = false;
            RefillPumpOn = false;
            FeedPumpOn = false;
            FanOn = false;
            Lights = new RgbColor[riserCount];
        }
    }
}
