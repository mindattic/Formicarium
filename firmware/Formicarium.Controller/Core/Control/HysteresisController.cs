using System;
using Formicarium.Core.Sensing;

namespace Formicarium.Core.Control
{
    public enum HysteresisMode
    {
        /// <summary>Turn on when the value falls low enough. Used by the heater.</summary>
        ActivateBelow,

        /// <summary>Turn on when the value rises high enough. Used by the circulation fan.</summary>
        ActivateAbove
    }

    /// <summary>
    /// Deadband on/off control with minimum dwell either side of a transition.
    ///
    /// One class serves both the heater and the fan because they are the same control with the
    /// comparison reversed, and sharing it means the dwell and fail-safe rules are written and
    /// tested once rather than twice.
    /// </summary>
    public sealed class HysteresisController
    {
        private readonly HysteresisMode _mode;
        private readonly double _onThreshold;
        private readonly double _offThreshold;
        private readonly double _plausibleMin;
        private readonly double _plausibleMax;
        private readonly int _minOnSeconds;
        private readonly int _minOffSeconds;
        private readonly int _maxAgeSeconds;

        private bool _isOn;
        private DateTime _lastTransitionUtc;
        private bool _hasTransitioned;

        public HysteresisController(
            HysteresisMode mode,
            double onThreshold,
            double offThreshold,
            double plausibleMin,
            double plausibleMax,
            int minOnSeconds,
            int minOffSeconds,
            int maxAgeSeconds)
        {
            _mode = mode;
            _onThreshold = onThreshold;
            _offThreshold = offThreshold;
            _plausibleMin = plausibleMin;
            _plausibleMax = plausibleMax;
            _minOnSeconds = minOnSeconds;
            _minOffSeconds = minOffSeconds;
            _maxAgeSeconds = maxAgeSeconds;
        }

        /// <summary>Boots off, like every actuator in this firmware.</summary>
        public bool IsOn
        {
            get { return _isOn; }
        }

        public bool Update(Reading reading, DateTime nowUtc)
        {
            // An unusable input fails safe to off IMMEDIATELY, bypassing the dwell timer.
            // Dwell exists to stop chatter between two states we are choosing between; it must
            // never delay a retreat to the safe state. A heater held on for another minute
            // because "it has not been on long enough to turn off" is exactly the bug this
            // ordering prevents.
            if (!reading.IsUsable(nowUtc, _maxAgeSeconds, _plausibleMin, _plausibleMax))
            {
                return ForceOff(nowUtc);
            }

            bool wantOn;

            if (_mode == HysteresisMode.ActivateBelow)
            {
                if (reading.Value <= _onThreshold)
                {
                    wantOn = true;
                }
                else if (reading.Value >= _offThreshold)
                {
                    wantOn = false;
                }
                else
                {
                    wantOn = _isOn; // inside the deadband: hold whatever we were doing
                }
            }
            else
            {
                if (reading.Value >= _onThreshold)
                {
                    wantOn = true;
                }
                else if (reading.Value <= _offThreshold)
                {
                    wantOn = false;
                }
                else
                {
                    wantOn = _isOn;
                }
            }

            if (wantOn == _isOn)
            {
                return _isOn;
            }

            if (_hasTransitioned)
            {
                double heldSeconds = (nowUtc - _lastTransitionUtc).TotalSeconds;
                int required = _isOn ? _minOnSeconds : _minOffSeconds;

                if (heldSeconds < required)
                {
                    return _isOn;
                }
            }

            _isOn = wantOn;
            _lastTransitionUtc = nowUtc;
            _hasTransitioned = true;
            return _isOn;
        }

        /// <summary>
        /// Drop to off now, whatever the dwell timer says. Used both for unusable readings and
        /// for the over-temperature cutout, which outranks normal control entirely.
        /// </summary>
        public bool ForceOff(DateTime nowUtc)
        {
            if (_isOn)
            {
                _isOn = false;
                _lastTransitionUtc = nowUtc;
                _hasTransitioned = true;
            }

            return false;
        }
    }
}
