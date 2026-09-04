using System;

namespace Formicarium.Core.Sensing
{
    /// <summary>
    /// A single sensor sample.
    ///
    /// The whole safety story of this firmware rests on one property of this type: a failed
    /// read produces an <b>invalid</b> Reading rather than a stale value. Controllers must
    /// never be able to act on a number that is no longer true, because the failure mode of
    /// "hold last known good" on a sealed, heated, misted nest is a cooked or flooded colony.
    ///
    /// <see cref="default"/> of this struct is invalid, which is the safe default.
    /// </summary>
    public struct Reading
    {
        public double Value { get; }
        public bool IsValid { get; }
        public DateTime TakenUtc { get; }

        private Reading(double value, bool isValid, DateTime takenUtc)
        {
            Value = value;
            IsValid = isValid;
            TakenUtc = takenUtc;
        }

        /// <summary>A successful read.</summary>
        public static Reading Good(double value, DateTime takenUtc)
        {
            return new Reading(value, true, takenUtc);
        }

        /// <summary>A failed read. Carries no value at all, deliberately.</summary>
        public static Reading Bad(DateTime takenUtc)
        {
            return new Reading(0.0, false, takenUtc);
        }

        public double AgeSeconds(DateTime nowUtc)
        {
            return (nowUtc - TakenUtc).TotalSeconds;
        }

        /// <summary>
        /// Valid, fresh, and inside the physically plausible range. All three must hold before
        /// any controller is allowed to act on this sample.
        ///
        /// The plausibility bound is not redundant with validity: a DS18B20 with a marginal
        /// connection reports 85.0 C (its power-on default) as a perfectly well-formed reading.
        /// Range-checking is what catches that.
        /// </summary>
        public bool IsUsable(DateTime nowUtc, int maxAgeSeconds, double plausibleMin, double plausibleMax)
        {
            if (!IsValid)
            {
                return false;
            }

            if (AgeSeconds(nowUtc) > maxAgeSeconds)
            {
                return false;
            }

            if (Value < plausibleMin || Value > plausibleMax)
            {
                return false;
            }

            return true;
        }
    }
}
