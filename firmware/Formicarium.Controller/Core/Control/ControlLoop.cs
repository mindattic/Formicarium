using System;
using Formicarium.Core.Actuation;
using Formicarium.Core.Config;
using Formicarium.Core.Sensing;
using Formicarium.Core.State;

namespace Formicarium.Core.Control
{
    /// <summary>
    /// The whole control policy, in one place, with no hardware types anywhere in it.
    ///
    /// Everything the device does on a tick is decided here and handed to an
    /// <see cref="IActuatorSet"/>, which is the only thing that knows about GPIO. That split is
    /// what makes the interesting half of this firmware testable on a desktop in milliseconds
    /// rather than on a bench over hours — and this is a system where the expensive failures
    /// are slow ones, so being able to fast-forward a week of simulated time matters.
    /// </summary>
    public sealed class ControlLoop
    {
        private readonly ISensorSet _sensors;
        private readonly IActuatorSet _actuators;
        private readonly Setpoints _setpoints;
        private readonly SafetyWatchdog _watchdog;
        private readonly HysteresisController _heater;
        private readonly HysteresisController _fan;
        private readonly HydrationController _hydration;
        private readonly FeedScheduler _feed;
        private readonly LightingController _lighting;
        private readonly RgbColor[] _darkness;
        private readonly DateTime _bootUtc;

        private bool _hasSeenAnyValidReading;

        public ControlLoop(
            ISensorSet sensors,
            IActuatorSet actuators,
            Setpoints setpoints,
            DateTime bootUtc,
            int riserCount)
        {
            _sensors = sensors;
            _actuators = actuators;
            _setpoints = setpoints;
            _bootUtc = bootUtc;

            _watchdog = new SafetyWatchdog(setpoints);

            _heater = new HysteresisController(
                HysteresisMode.ActivateBelow,
                setpoints.NestHeatOnBelowC,
                setpoints.NestHeatOffAboveC,
                setpoints.TempPlausibleMinC,
                setpoints.TempPlausibleMaxC,
                setpoints.HeaterMinOnSeconds,
                setpoints.HeaterMinOffSeconds,
                setpoints.MaxReadingAgeSeconds);

            _fan = new HysteresisController(
                HysteresisMode.ActivateAbove,
                setpoints.FanOnAbovePercentRh,
                setpoints.FanOffBelowPercentRh,
                setpoints.HumidityPlausibleMin,
                setpoints.HumidityPlausibleMax,
                setpoints.FanMinOnSeconds,
                setpoints.FanMinOffSeconds,
                setpoints.MaxReadingAgeSeconds);

            _hydration = new HydrationController(setpoints);
            _feed = new FeedScheduler(setpoints);
            _lighting = new LightingController(setpoints, riserCount);

            _darkness = new RgbColor[riserCount];

            // Boot state is everything off. The gate pulldowns hold the MOSFETs off through
            // reset before this line runs; this makes it true in software too.
            _actuators.AllOff();
        }

        /// <summary>
        /// Holds every output off so the column can be handled. This is not a gate — nothing is
        /// closed, only stopped. Physical containment during service is the manual riser plug
        /// caps, which cannot fail closed while nobody is watching.
        /// </summary>
        public bool ServiceMode { get; set; }

        public LightingController Lighting
        {
            get { return _lighting; }
        }

        public HydrationController Hydration
        {
            get { return _hydration; }
        }

        public FeedScheduler Feed
        {
            get { return _feed; }
        }

        public Setpoints Setpoints
        {
            get { return _setpoints; }
        }

        public DeviceState Tick(DateTime nowUtc)
        {
            SensorSnapshot snapshot = _sensors.Poll(nowUtc);

            if (SafetyWatchdog.AnyReadingValid(snapshot))
            {
                _hasSeenAnyValidReading = true;
            }

            Faults faults = _watchdog.Evaluate(snapshot, nowUtc, _hydration, _hasSeenAnyValidReading);

            bool heaterOn;
            bool refillOn;
            bool fanOn;
            bool feedOn;
            RgbColor[] colors;

            // Two conditions stop everything outright. Service mode is a human asking; the
            // other is the controller admitting it has never successfully read a single sensor,
            // in which case it has no business energising a heater in a sealed box.
            if (ServiceMode || !_hasSeenAnyValidReading)
            {
                heaterOn = _heater.ForceOff(nowUtc);
                fanOn = _fan.ForceOff(nowUtc);
                refillOn = false;
                feedOn = false;
                colors = _darkness;
                _actuators.AllOff();
            }
            else
            {
                heaterOn = _heater.Update(snapshot.NestBottomTempC, nowUtc);

                // The cutout outranks normal control, and it reads every nest probe rather than
                // only the one the heater controls from.
                if (SafetyWatchdog.Has(faults, Faults.NestOverTemp))
                {
                    heaterOn = _heater.ForceOff(nowUtc);
                }

                refillOn = _hydration.Update(snapshot.ReservoirFull, snapshot.SoilMoisturePct, nowUtc);
                fanOn = _fan.Update(snapshot.OutworldHumidityPct, nowUtc);
                feedOn = _feed.Update(nowUtc);
                colors = _lighting.Update(snapshot.RiserRatesPerMinute, nowUtc);

                _actuators.SetHeater(heaterOn);
                _actuators.SetRefillPump(refillOn);
                _actuators.SetFeedPump(feedOn);
                _actuators.SetFan(fanOn);
                _actuators.SetRiserLights(colors);
            }

            return BuildState(snapshot, nowUtc, faults, heaterOn, refillOn, feedOn, fanOn);
        }

        /// <summary>Dashboard-triggered sugar-water dose. Rate limited inside the scheduler.</summary>
        public bool RequestManualFeed(DateTime nowUtc)
        {
            if (ServiceMode)
            {
                return false;
            }

            return _feed.RequestManualDose(nowUtc);
        }

        private DeviceState BuildState(
            SensorSnapshot snapshot,
            DateTime nowUtc,
            Faults faults,
            bool heaterOn,
            bool refillOn,
            bool feedOn,
            bool fanOn)
        {
            DeviceState state = new DeviceState();

            state.TimestampUnixMs = (long)((nowUtc - new DateTime(1970, 1, 1)).TotalMilliseconds);
            state.UptimeSeconds = (long)((nowUtc - _bootUtc).TotalSeconds);

            state.NestBottomTempC = snapshot.NestBottomTempC.Value;
            state.NestBottomTempOk = snapshot.NestBottomTempC.IsValid;
            state.NestTopTempC = snapshot.NestTopTempC.Value;
            state.NestTopTempOk = snapshot.NestTopTempC.IsValid;
            state.OutworldTempC = snapshot.OutworldTempC.Value;
            state.OutworldTempOk = snapshot.OutworldTempC.IsValid;

            state.NestHumidityPct = snapshot.NestHumidityPct.Value;
            state.NestHumidityOk = snapshot.NestHumidityPct.IsValid;
            state.OutworldHumidityPct = snapshot.OutworldHumidityPct.Value;
            state.OutworldHumidityOk = snapshot.OutworldHumidityPct.IsValid;

            state.SoilMoisturePct = snapshot.SoilMoisturePct.Value;
            state.SoilMoistureOk = snapshot.SoilMoisturePct.IsValid;

            state.RiserCounts = snapshot.RiserCounts;
            state.RiserRatesPerMinute = snapshot.RiserRatesPerMinute;
            state.RiserDebouncedCounts = snapshot.RiserDebouncedCounts;
            state.RiserChannel = _lighting.ChannelForRiser;
            state.RiserBrightness = _lighting.BrightnessFraction;
            state.AssignmentDayNumber = _lighting.AssignmentDayNumber;
            state.LightingMode = (int)_lighting.Mode;
            state.LightCurfewActive = _lighting.InCurfew;

            state.HeaterOn = heaterOn;
            state.RefillPumpOn = refillOn;
            state.FeedPumpOn = feedOn;
            state.FanOn = fanOn;
            state.RefillWanted = _hydration.RefillWanted;
            state.ReservoirFull = snapshot.ReservoirFull;

            state.Faults = (int)faults;
            state.ServiceMode = ServiceMode;

            return state;
        }
    }
}
