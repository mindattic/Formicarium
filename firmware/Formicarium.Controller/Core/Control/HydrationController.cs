using System;
using Formicarium.Core.Config;
using Formicarium.Core.Sensing;

namespace Formicarium.Core.Control
{
    /// <summary>
    /// Keeps the passive hydration reservoir topped up.
    ///
    /// This replaced a controller that pumped water directly into the nest substrate, and the
    /// reason is worth recording, because it is the same argument that removed the servo gate.
    ///
    /// With the gate gone, a pump aimed at a sealed, drainless nest was the largest remaining
    /// single point of failure. A maximum-runtime limit bounds it in <i>software</i>, which is
    /// no help at all against the failures that actually flood a nest: a MOSFET that fails
    /// short, a pump head that jams open, or a reservoir mounted above the nest quietly
    /// siphoning through a stopped pump. None of those are things firmware gets a vote on.
    ///
    /// So the pump no longer touches the nest. It fills a small reservoir that wicks into the
    /// Ytong nest core, and that reservoir holds less water than would harm the colony even if
    /// the whole thing emptied into the nest at once. The guarantee moved out of the code and
    /// into the geometry, where it cannot be defeated by an electrical fault.
    ///
    /// A second property falls out of this for free, and it is a large one: <b>refilling no
    /// longer depends on the soil probe</b>. It keys off a float switch in the reservoir, so a
    /// dead moisture probe degrades the system to "passively hydrated, unmonitored" rather than
    /// to "not hydrated". Under the old design a failed probe stopped watering entirely.
    /// </summary>
    public sealed class HydrationController
    {
        private readonly Setpoints _setpoints;

        private bool _isRefilling;
        private DateTime _refillStartedUtc;
        private DateTime _lastRefillEndedUtc;
        private bool _hasEverRefilled;
        private int _consecutiveCutoffs;

        public HydrationController(Setpoints setpoints)
        {
            _setpoints = setpoints;
        }

        public bool IsRefilling
        {
            get { return _isRefilling; }
        }

        /// <summary>Reservoir is below full. Reported separately from <see cref="IsRefilling"/> so a cooldown hold is visible.</summary>
        public bool RefillWanted { get; private set; }

        /// <summary>
        /// Latches when a refill run is stopped by the hard cutoff instead of by the reservoir
        /// reading full. Means the float switch is stuck, the pump is not moving water, or the
        /// supply is dry — all of which need a human.
        /// </summary>
        public bool HitMaxRuntime { get; private set; }

        /// <summary>
        /// Several refill attempts in a row have timed out without the reservoir filling. The
        /// overwhelmingly likely cause is that the supply container is empty, which is the one
        /// piece of routine maintenance this design still needs.
        /// </summary>
        public bool WaterSupplyEmpty { get; private set; }

        /// <summary>Substrate is wetter than it should be — wicking is running away, or the probe is wrong.</summary>
        public bool SubstrateOverWet { get; private set; }

        /// <summary>Substrate is dry despite the reservoir being kept full, so wicking has failed.</summary>
        public bool SubstrateDry { get; private set; }

        public double LastRefillSeconds { get; private set; }

        public bool Update(bool reservoirFull, Reading soilMoisture, DateTime nowUtc)
        {
            bool soilUsable = soilMoisture.IsUsable(
                nowUtc,
                _setpoints.MaxReadingAgeSeconds,
                _setpoints.MoisturePlausibleMin,
                _setpoints.MoisturePlausibleMax);

            SubstrateOverWet = soilUsable && soilMoisture.Value >= _setpoints.SubstrateOverWetPercent;
            SubstrateDry = soilUsable && soilMoisture.Value <= _setpoints.SubstrateDryPercent;

            RefillWanted = !reservoirFull;

            if (_isRefilling)
            {
                if (reservoirFull)
                {
                    _consecutiveCutoffs = 0;
                    WaterSupplyEmpty = false;
                    return Stop(nowUtc, false);
                }

                double elapsed = (nowUtc - _refillStartedUtc).TotalSeconds;

                if (elapsed >= _setpoints.RefillMaxRunSeconds)
                {
                    return Stop(nowUtc, true);
                }

                return true;
            }

            if (reservoirFull)
            {
                return false;
            }

            // An over-wet nest means water is already going somewhere it should not. Adding more
            // to the reservoir would still be harmless — that is the whole point of sizing it —
            // but there is no reason to, and holding off makes the fault easier to diagnose.
            if (SubstrateOverWet)
            {
                return false;
            }

            if (_hasEverRefilled)
            {
                double sinceLast = (nowUtc - _lastRefillEndedUtc).TotalSeconds;

                if (sinceLast < _setpoints.RefillCooldownSeconds)
                {
                    return false;
                }
            }

            _isRefilling = true;
            _refillStartedUtc = nowUtc;
            return true;
        }

        /// <summary>Clears the latched refill and supply faults once a human has refilled or investigated.</summary>
        public void Acknowledge()
        {
            HitMaxRuntime = false;
            WaterSupplyEmpty = false;
            _consecutiveCutoffs = 0;
        }

        private bool Stop(DateTime nowUtc, bool becauseMaxRuntime)
        {
            if (_isRefilling)
            {
                LastRefillSeconds = (nowUtc - _refillStartedUtc).TotalSeconds;
                _isRefilling = false;
                _lastRefillEndedUtc = nowUtc;
                _hasEverRefilled = true;

                if (becauseMaxRuntime)
                {
                    HitMaxRuntime = true;
                    _consecutiveCutoffs++;

                    if (_consecutiveCutoffs >= _setpoints.RefillCutoffsBeforeSupplyAlarm)
                    {
                        WaterSupplyEmpty = true;
                    }
                }
            }

            return false;
        }
    }
}
