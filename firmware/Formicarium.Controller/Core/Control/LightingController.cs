using System;
using Formicarium.Core.Actuation;
using Formicarium.Core.Config;
using Formicarium.Core.Sensing;

namespace Formicarium.Core.Control
{
    public enum LightingMode
    {
        /// <summary>Dark. The control condition: traffic is logged with no light present at all.</summary>
        Off,

        /// <summary>
        /// All rings lit at a constant equal brightness in their assigned colours. The second
        /// control condition: colour is present but carries no information about traffic. This
        /// is what separates "ants respond to colour" from "ants respond to light changing".
        /// </summary>
        Fixed,

        /// <summary>
        /// The experiment. Each ring's brightness tracks its own riser's traffic, so the light
        /// at the choice point is a function of the choices already made.
        /// </summary>
        ClosedLoop
    }

    /// <summary>
    /// Drives the three riser-mouth rings.
    ///
    /// Each riser owns one of the red, green and blue channels, and in
    /// <see cref="LightingMode.ClosedLoop"/> that channel's brightness follows the riser's own
    /// traffic rate. The rings sit in the sealed electronics bay and shine up through frosted
    /// windows in the outworld floor, one around each riser mouth, so the stimulus lands
    /// exactly where an ant chooses a tube. Lighting the chamber as a whole would not work:
    /// an ant at the mouths would see the same blended colour whichever tube it picked, so the
    /// preference would have nothing to act through.
    ///
    /// What this actually measures is worth stating plainly. Ants are most sensitive to UV and
    /// blue-green and are effectively blind to deep red, so red, green and blue are three very
    /// different stimulus strengths rather than three equivalent flavours. The expected result
    /// is negative feedback on blue, weak feedback on green and almost none on red — which
    /// predicts the red riser drifting to dominance.
    ///
    /// That prediction is only testable because of the daily rotation below. Without it,
    /// "they prefer green" and "they prefer the tube nearest the food dish" produce identical
    /// data and the experiment means nothing.
    /// </summary>
    public sealed class LightingController
    {
        private const int ChannelCount = 3;

        private readonly Setpoints _setpoints;
        private readonly int _riserCount;
        private readonly int[] _channelForRiser;
        private readonly RgbColor[] _colors;
        private readonly double[] _brightnessFraction;

        public LightingController(Setpoints setpoints, int riserCount)
        {
            _setpoints = setpoints;
            _riserCount = riserCount;
            _channelForRiser = new int[riserCount];
            _colors = new RgbColor[riserCount];
            _brightnessFraction = new double[riserCount];
            Mode = LightingMode.ClosedLoop;
        }

        public LightingMode Mode { get; set; }

        /// <summary>
        /// Which channel each riser currently owns: 0 red, 1 green, 2 blue. Logged with every
        /// traffic sample so the mapping can be recovered during analysis.
        /// </summary>
        public int[] ChannelForRiser
        {
            get { return _channelForRiser; }
        }

        /// <summary>Local day number the current mapping derives from. Changes at local midnight.</summary>
        public int AssignmentDayNumber { get; private set; }

        /// <summary>True while the guaranteed dark period is in force, whatever the mode says.</summary>
        public bool InCurfew { get; private set; }

        public RgbColor[] Update(double[] riserRatesPerMinute, DateTime nowUtc)
        {
            int dayNumber = LocalTime.DayNumber(nowUtc, _setpoints.UtcOffsetMinutes);
            DateTime local = LocalTime.FromUtc(nowUtc, _setpoints.UtcOffsetMinutes);

            // Cyclic rotation keyed on the local day number, so it advances at local midnight
            // and is reproducible from a timestamp alone rather than from controller uptime.
            // With three risers each one holds each colour every third day.
            AssignmentDayNumber = dayNumber;

            for (int riser = 0; riser < _riserCount; riser++)
            {
                int shift = ((dayNumber % ChannelCount) + ChannelCount) % ChannelCount;
                _channelForRiser[riser] = (riser + shift) % ChannelCount;
            }

            InCurfew = IsInCurfew(local.Hour);

            if (Mode == LightingMode.Off || InCurfew)
            {
                for (int riser = 0; riser < _riserCount; riser++)
                {
                    _brightnessFraction[riser] = 0.0;
                    _colors[riser] = RgbColor.Black;
                }

                return _colors;
            }

            for (int riser = 0; riser < _riserCount; riser++)
            {
                double fraction;

                if (Mode == LightingMode.Fixed)
                {
                    // Half scale, chosen to sit near the mean the closed loop produces, so the
                    // two lit conditions deliver a comparable photon dose and differ only in
                    // whether the light carries information.
                    fraction = 0.5;
                }
                else
                {
                    double rate = riser < riserRatesPerMinute.Length ? riserRatesPerMinute[riser] : 0.0;
                    double normalised = rate / _setpoints.LightFullScaleBreaksPerMinute;
                    normalised = Clamp01(normalised);

                    // Linear by default: the stimulus an ant receives is closer to photon dose
                    // than to perceived brightness, so the duty tracks traffic directly. See
                    // Setpoints.LightResponseExponent for why this is not gamma-corrected.
                    fraction = _setpoints.LightResponseExponent == 1.0
                        ? normalised
                        : Math.Pow(normalised, _setpoints.LightResponseExponent);
                }

                _brightnessFraction[riser] = fraction;

                byte level = (byte)(fraction * _setpoints.LightMaxBrightness);
                _colors[riser] = ColorFor(_channelForRiser[riser], level);
            }

            return _colors;
        }

        /// <summary>Brightness fraction per riser, 0-1. Reported so the dashboard can plot the stimulus alongside the response.</summary>
        public double[] BrightnessFraction
        {
            get { return _brightnessFraction; }
        }

        private bool IsInCurfew(int localHour)
        {
            int start = _setpoints.LightCurfewStartHourLocal;
            int end = _setpoints.LightCurfewEndHourLocal;

            if (start == end)
            {
                return false;
            }

            if (start < end)
            {
                return localHour >= start && localHour < end;
            }

            // Wraps midnight, which is the normal case for a night-time curfew.
            return localHour >= start || localHour < end;
        }

        private static RgbColor ColorFor(int channel, byte level)
        {
            if (channel == 0)
            {
                return new RgbColor(level, 0, 0);
            }

            if (channel == 1)
            {
                return new RgbColor(0, level, 0);
            }

            return new RgbColor(0, 0, level);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            if (value > 1.0)
            {
                return 1.0;
            }

            return value;
        }
    }
}
