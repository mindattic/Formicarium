using System;
using Formicarium.Core.Config;
using Formicarium.Core.Sensing;

namespace Formicarium.Core.Control
{
    /// <summary>
    /// Fault bits. Power-of-two values, combined bitwise. Serialised to the API as a plain
    /// integer so the dashboard owns the presentation and the firmware stays small.
    /// </summary>
    public enum Faults
    {
        None = 0,
        NestBottomTempUnusable = 1,
        NestTopTempUnusable = 2,
        OutworldTempUnusable = 4,
        NestHumidityUnusable = 8,
        OutworldHumidityUnusable = 16,
        SoilMoistureUnusable = 32,

        /// <summary>A nest probe is above the hard cutout. Heater is off regardless of control state.</summary>
        NestOverTemp = 64,

        /// <summary>A reservoir refill was ended by the hard cutoff instead of by the reservoir filling.</summary>
        RefillCutoff = 128,

        /// <summary>Not one sensor has produced a usable reading since boot. Everything stays off.</summary>
        NoValidReadingSinceBoot = 256,

        /// <summary>Several refills in a row timed out. The supply container is almost certainly empty.</summary>
        WaterSupplyEmpty = 512,

        /// <summary>Nest is wetter than it should be. Wicking is running away, or the probe is wrong.</summary>
        SubstrateOverWet = 1024,

        /// <summary>Nest is dry despite a full reservoir, so wicking has failed. Alert only.</summary>
        SubstrateDry = 2048
    }

    /// <summary>
    /// Turns a sensor snapshot into a fault bitmask, and answers the one question the control
    /// loop needs from it: is the heater permitted right now.
    ///
    /// The individual controllers already fail safe on their own sensor going bad. This exists
    /// for the conditions that span sensors — the over-temperature cutout reads every nest
    /// probe, not just the control probe, so a failed control probe reading plausibly low
    /// cannot cook the colony while a second probe watches it happen.
    /// </summary>
    public sealed class SafetyWatchdog
    {
        private readonly Setpoints _setpoints;

        public SafetyWatchdog(Setpoints setpoints)
        {
            _setpoints = setpoints;
        }

        public Faults Evaluate(
            SensorSnapshot snapshot,
            DateTime nowUtc,
            HydrationController hydration,
            bool hasSeenAnyValidReading)
        {
            Faults faults = Faults.None;

            int maxAge = _setpoints.MaxReadingAgeSeconds;
            double tMin = _setpoints.TempPlausibleMinC;
            double tMax = _setpoints.TempPlausibleMaxC;
            double hMin = _setpoints.HumidityPlausibleMin;
            double hMax = _setpoints.HumidityPlausibleMax;

            bool nestBottomOk = snapshot.NestBottomTempC.IsUsable(nowUtc, maxAge, tMin, tMax);
            bool nestTopOk = snapshot.NestTopTempC.IsUsable(nowUtc, maxAge, tMin, tMax);

            if (!nestBottomOk)
            {
                faults |= Faults.NestBottomTempUnusable;
            }

            if (!nestTopOk)
            {
                faults |= Faults.NestTopTempUnusable;
            }

            if (!snapshot.OutworldTempC.IsUsable(nowUtc, maxAge, tMin, tMax))
            {
                faults |= Faults.OutworldTempUnusable;
            }

            if (!snapshot.NestHumidityPct.IsUsable(nowUtc, maxAge, hMin, hMax))
            {
                faults |= Faults.NestHumidityUnusable;
            }

            if (!snapshot.OutworldHumidityPct.IsUsable(nowUtc, maxAge, hMin, hMax))
            {
                faults |= Faults.OutworldHumidityUnusable;
            }

            if (!snapshot.SoilMoisturePct.IsUsable(
                    nowUtc, maxAge, _setpoints.MoisturePlausibleMin, _setpoints.MoisturePlausibleMax))
            {
                faults |= Faults.SoilMoistureUnusable;
            }

            // Over-temperature is checked against EVERY usable nest probe, not just the one the
            // heater controls from. A control probe that fails low while still reporting
            // plausible values would otherwise call for heat indefinitely.
            bool overTemp =
                (nestBottomOk && snapshot.NestBottomTempC.Value > _setpoints.NestOverTempCutoutC) ||
                (nestTopOk && snapshot.NestTopTempC.Value > _setpoints.NestOverTempCutoutC);

            if (overTemp)
            {
                faults |= Faults.NestOverTemp;
            }

            if (hydration.HitMaxRuntime)
            {
                faults |= Faults.RefillCutoff;
            }

            if (hydration.WaterSupplyEmpty)
            {
                faults |= Faults.WaterSupplyEmpty;
            }

            if (hydration.SubstrateOverWet)
            {
                faults |= Faults.SubstrateOverWet;
            }

            if (hydration.SubstrateDry)
            {
                faults |= Faults.SubstrateDry;
            }

            if (!hasSeenAnyValidReading)
            {
                faults |= Faults.NoValidReadingSinceBoot;
            }

            return faults;
        }

        public static bool Has(Faults faults, Faults flag)
        {
            return (faults & flag) != 0;
        }

        /// <summary>
        /// Did any sensor at all read successfully this pass. Used to hold every output off
        /// until the hardware has proved itself once after boot.
        /// </summary>
        public static bool AnyReadingValid(SensorSnapshot snapshot)
        {
            return snapshot.NestBottomTempC.IsValid
                   || snapshot.NestTopTempC.IsValid
                   || snapshot.OutworldTempC.IsValid
                   || snapshot.NestHumidityPct.IsValid
                   || snapshot.OutworldHumidityPct.IsValid
                   || snapshot.SoilMoisturePct.IsValid;
        }
    }
}
