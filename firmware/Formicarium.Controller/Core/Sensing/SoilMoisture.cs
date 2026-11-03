namespace Formicarium.Core.Sensing
{
    /// <summary>
    /// Turns a calibrated soil-moisture percentage into the 0-100 scale control logic expects.
    ///
    /// A capacitive probe that sits a little wetter than the "wet" calibration point reads a few
    /// percent over 100. Left as-is, the plausibility check (0-100) rejects it as unusable, which
    /// switches off over-wet detection exactly when the nest is wettest. A small overshoot is
    /// therefore clamped to 100 (fully wet, which raises the over-wet hold), while a large one
    /// is passed through so the plausibility check still rejects a probe that is plainly wrong
    /// (a shorted or disconnected sensor, a bad calibration).
    ///
    /// Only the wet side is clamped. A reading below 0 still fails plausibility: treating it as
    /// "bone dry" would ask for water on a guess.
    /// </summary>
    public static class SoilMoisture
    {
        /// <summary>Readings above 100 up to this are calibration overshoot, clamped to 100.</summary>
        public const double WetOvershootLimitPercent = 110.0;

        public static double ClampCalibrationOvershoot(double percent)
        {
            if (percent > 100.0 && percent <= WetOvershootLimitPercent)
            {
                return 100.0;
            }

            return percent;
        }
    }
}
