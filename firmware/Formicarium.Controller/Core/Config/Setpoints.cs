namespace Formicarium.Core.Config
{
    /// <summary>
    /// Every tunable in one place. Defaults target <i>Tetramorium immigrans</i>, which needs no
    /// diapause, so these are held flat year-round rather than seasonally scheduled.
    ///
    /// Temperatures are Celsius internally and converted only at the UI edge.
    /// </summary>
    public sealed class Setpoints
    {
        // --- Time ------------------------------------------------------------------------
        /// <summary>
        /// Minutes to add to UTC to get local time. The ESP32 has no timezone database, so
        /// this is a fixed offset and does not follow DST. Only the daily feed and the
        /// midnight colour rotation depend on it.
        /// </summary>
        public int UtcOffsetMinutes { get; set; } = -300; // US Eastern, standard time

        /// <summary>
        /// A reading older than this is treated as no reading at all. At a 1 Hz control loop
        /// this is generously slack; it exists to catch a sensor that has stopped responding
        /// rather than one that is merely slow.
        /// </summary>
        public int MaxReadingAgeSeconds { get; set; } = 30;

        // --- Heating ---------------------------------------------------------------------
        // The heat cable wraps the OUTSIDE of the Colony section, low down. Control is taken
        // from the nest-bottom probe because it is closest to the heater and therefore both
        // the fastest-responding and the overheat-limiting measurement. The nest-top probe is
        // deliberately not controlled to: the difference between them IS the vertical gradient
        // the colony wants, so forcing the top to setpoint would flatten it.
        public double NestHeatOnBelowC { get; set; } = 26.1;   // 79 F
        public double NestHeatOffAboveC { get; set; } = 28.3;  // 83 F

        /// <summary>Hard cutout. Any nest probe above this kills the heater regardless of state.</summary>
        public double NestOverTempCutoutC { get; set; } = 35.0;

        public double TempPlausibleMinC { get; set; } = -5.0;
        public double TempPlausibleMaxC { get; set; } = 60.0;

        /// <summary>
        /// Minimum dwell either side of a heater transition. Prevents chatter at the setpoint
        /// edge, where sensor noise alone would otherwise toggle the output every loop.
        /// </summary>
        public int HeaterMinOnSeconds { get; set; } = 60;
        public int HeaterMinOffSeconds { get; set; } = 60;

        // --- Hydration -------------------------------------------------------------------
        // The pump fills a small reservoir; the reservoir wicks into the Ytong nest core. It
        // never sprays the nest. See HydrationController for why that distinction carries the
        // whole safety argument.
        //
        // Soil moisture is normalised to 0-100 with HIGHER MEANING WETTER by the hardware
        // adapter, which inverts and calibrates the raw capacitive reading. Control logic never
        // sees raw ADC counts.
        public double MoisturePlausibleMin { get; set; } = 0.0;
        public double MoisturePlausibleMax { get; set; } = 100.0;

        /// <summary>
        /// Above this the nest is wetter than it should be, meaning wicking is running away or
        /// the probe is wrong. Refills are held off and a fault is raised.
        /// </summary>
        public double SubstrateOverWetPercent { get; set; } = 85.0;

        /// <summary>
        /// Below this the nest is dry even though the reservoir is being kept full, so wicking
        /// has failed - a dried-out contact face, or a probe that has worked loose from the core.
        /// Purely an alert: no actuator can fix it.
        /// </summary>
        public double SubstrateDryPercent { get; set; } = 25.0;

        /// <summary>
        /// Hard cutoff on a single refill run. Long enough to fill the reservoir from empty,
        /// short enough that a stuck float switch cannot run the pump indefinitely.
        /// </summary>
        public int RefillMaxRunSeconds { get; set; } = 45;

        /// <summary>Minimum gap between refill attempts.</summary>
        public int RefillCooldownSeconds { get; set; } = 600;

        /// <summary>
        /// Consecutive timed-out refills before declaring the supply container empty. More than
        /// one, because a single timeout can be an air lock clearing itself.
        /// </summary>
        public int RefillCutoffsBeforeSupplyAlarm { get; set; } = 3;

        // --- Circulation -----------------------------------------------------------------
        // Gated on outworld humidity rather than run continuously: constant airflow would dry
        // the nest through the risers.
        public double FanOnAbovePercentRh { get; set; } = 80.0;
        public double FanOffBelowPercentRh { get; set; } = 70.0;
        public double HumidityPlausibleMin { get; set; } = 0.0;
        public double HumidityPlausibleMax { get; set; } = 100.0;
        public int FanMinOnSeconds { get; set; } = 30;
        public int FanMinOffSeconds { get; set; } = 30;

        // --- Feeding ---------------------------------------------------------------------
        // Sugar water only. Protein feeding stays manual by design.
        public int FeedHourLocal { get; set; } = 9;
        public int FeedMinuteLocal { get; set; } = 0;
        public int FeedDoseSeconds { get; set; } = 8;

        /// <summary>Rate limit on dashboard-triggered doses, so the dish cannot be flooded by clicking.</summary>
        public int FeedManualCooldownSeconds { get; set; } = 3600;

        // --- Riser traffic ---------------------------------------------------------------
        /// <summary>
        /// Minimum gap between two counted beam breaks on one riser. A single ant occludes the
        /// beam for tens of milliseconds and its legs and antennae can chatter the receiver, so
        /// without debounce one ant reads as several.
        /// </summary>
        public int BeamDebounceMs { get; set; } = 120;

        /// <summary>Window over which the per-riser traffic rate is averaged.</summary>
        public int TrafficWindowSeconds { get; set; } = 300;

        // --- Riser lighting --------------------------------------------------------------
        /// <summary>
        /// Per-riser traffic rate that maps to full brightness. Above this the ring saturates.
        ///
        /// This is a per-tube figure, not a colony total. A Tetramorium colony at peak foraging
        /// puts on the order of ten crossings a minute through its favoured tube, so setting this
        /// to a colony-wide number leaves the rings dark at all realistic activity levels and the
        /// experiment never applies a stimulus at all.
        /// </summary>
        public double LightFullScaleBreaksPerMinute { get; set; } = 12.0;

        /// <summary>
        /// Exponent applied to the normalised traffic rate before it becomes a duty cycle.
        ///
        /// Deliberately 1.0 — linear. Gamma-correcting would be right if a human were reading
        /// these rings as a display, because eyes are not linear in emitted power. But the
        /// audience here is an ant and the quantity that matters is the photon dose delivered at
        /// the choice point, so a 2.2 exponent just crushes the bottom two thirds of the range
        /// and throws away the experiment's dynamic range. Exposed as a setpoint so the response
        /// curve can be retuned against a real colony without a firmware change.
        /// </summary>
        public double LightResponseExponent { get; set; } = 1.0;

        /// <summary>Ceiling on ring brightness, 0-255. Kept low: this is a stimulus, not illumination.</summary>
        public byte LightMaxBrightness { get; set; } = 90;

        /// <summary>
        /// Guaranteed dark period. The colony gets an unlit night regardless of activity or
        /// mode, so the experiment never removes the option of darkness.
        /// </summary>
        public int LightCurfewStartHourLocal { get; set; } = 22;
        public int LightCurfewEndHourLocal { get; set; } = 7;
    }
}
