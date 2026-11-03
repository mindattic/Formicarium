using System;
using Formicarium.Core.Config;
using Formicarium.Core.Sensing;

namespace Formicarium.Core.Control
{
    /// <summary>
    /// Timed sugar-water dosing into the outworld dish.
    ///
    /// Unlike the mister this has no sensor to react to, so the only safety it needs is that a
    /// dose is bounded and happens at most once per local day. The daily key is the local day
    /// number rather than an elapsed-time counter, so a controller reboot does not produce a
    /// second breakfast.
    ///
    /// Without a wall clock (SNTP has not landed yet) there is no scheduled dose at all: the
    /// colony simply misses breakfast until the clock is set, which is the safe direction for a
    /// small dish. Once it is set, the first-pass rule below treats a slot that already went by
    /// today as served, so a late clock jump cannot dose immediately. Manual doses still work.
    ///
    /// Protein feeding stays manual by design — fruit flies are not worth automating, and a
    /// jammed protein feeder would rot in the dish.
    /// </summary>
    public sealed class FeedScheduler
    {
        private readonly Setpoints _setpoints;

        private bool _isDosing;
        private DateTime _doseStartedUtc;
        private int _lastScheduledDoseDay = int.MinValue;
        private DateTime _lastManualDoseUtc;
        private bool _hasManualDosed;
        private bool _initialised;

        public FeedScheduler(Setpoints setpoints)
        {
            _setpoints = setpoints;
        }

        public bool IsDosing
        {
            get { return _isDosing; }
        }

        public bool LastDoseWasManual { get; private set; }

        public bool Update(DateTime nowUtc)
        {
            if (_isDosing)
            {
                double elapsed = (nowUtc - _doseStartedUtc).TotalSeconds;

                if (elapsed >= _setpoints.FeedDoseSeconds)
                {
                    _isDosing = false;
                    return false;
                }

                return true;
            }

            // No scheduled dose against an unset clock. Program runs the loop headless when SNTP
            // has not landed, and the clock can jump to real time later; if the first pass below
            // had already run on the epoch date, that jump would look like a fresh, due day and
            // dose immediately - the reboot-double-dose the first-pass rule exists to prevent.
            if (!LocalTime.IsWallClockSet(nowUtc))
            {
                return false;
            }

            DateTime local = LocalTime.FromUtc(nowUtc, _setpoints.UtcOffsetMinutes);
            int today = LocalTime.DayNumber(nowUtc, _setpoints.UtcOffsetMinutes);

            bool dueNow =
                local.Hour > _setpoints.FeedHourLocal ||
                (local.Hour == _setpoints.FeedHourLocal && local.Minute >= _setpoints.FeedMinuteLocal);

            // On the very first pass, treat a slot that has already gone by today as already
            // served. Without this, a controller that reboots at any point after the feed hour
            // doses again on startup, so a boot loop — a brownout, a flaky supply, a watchdog
            // reset — empties the syrup reservoir into the dish.
            if (!_initialised)
            {
                _initialised = true;

                if (dueNow)
                {
                    _lastScheduledDoseDay = today;
                    return false;
                }
            }

            if (today == _lastScheduledDoseDay)
            {
                return false;
            }

            if (!dueNow)
            {
                return false;
            }

            _lastScheduledDoseDay = today;
            _isDosing = true;
            _doseStartedUtc = nowUtc;
            LastDoseWasManual = false;
            return true;
        }

        /// <summary>
        /// Dashboard-triggered dose. Rate limited, because the dish is small and the failure
        /// mode of an over-eager button is a drowned outworld.
        /// </summary>
        public bool RequestManualDose(DateTime nowUtc)
        {
            if (_isDosing)
            {
                return false;
            }

            if (_hasManualDosed &&
                (nowUtc - _lastManualDoseUtc).TotalSeconds < _setpoints.FeedManualCooldownSeconds)
            {
                return false;
            }

            _isDosing = true;
            _doseStartedUtc = nowUtc;
            _lastManualDoseUtc = nowUtc;
            _hasManualDosed = true;
            LastDoseWasManual = true;
            return true;
        }
    }
}
