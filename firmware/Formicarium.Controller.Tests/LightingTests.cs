using System;
using Formicarium.Core.Actuation;
using Formicarium.Core.Config;
using Formicarium.Core.Control;
using Xunit;

namespace Formicarium.Controller.Tests
{
    /// <summary>
    /// The riser lighting is a behavioural experiment, not decoration, so what is pinned here
    /// is mostly experimental validity: that the daily rotation really is a rotation, that the
    /// dark control is genuinely dark, and that the colony always gets a night.
    /// </summary>
    public class LightingTests
    {
        // 16:00 UTC is 11:00 local at the default -300 offset: daytime, well clear of both the
        // 22:00-07:00 curfew and local midnight.
        private static readonly DateTime Midday = new DateTime(2026, 6, 1, 16, 0, 0);

        private static double[] Rates(double a, double b, double c)
        {
            return new double[] { a, b, c };
        }

        private static int[] Snapshot(int[] source)
        {
            int[] copy = new int[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        [Fact]
        public void EachRiserHoldsADistinctChannel()
        {
            LightingController lighting = new LightingController(new Setpoints(), 3);
            lighting.Update(Rates(10, 10, 10), Midday);

            int[] channels = lighting.ChannelForRiser;

            Assert.Equal(3, channels.Length);
            Assert.NotEqual(channels[0], channels[1]);
            Assert.NotEqual(channels[1], channels[2]);
            Assert.NotEqual(channels[0], channels[2]);
        }

        [Fact]
        public void MappingRotatesEveryDayAndReturnsAfterThree()
        {
            LightingController lighting = new LightingController(new Setpoints(), 3);

            lighting.Update(Rates(1, 1, 1), Midday);
            int[] day0 = Snapshot(lighting.ChannelForRiser);

            lighting.Update(Rates(1, 1, 1), Midday.AddDays(1));
            int[] day1 = Snapshot(lighting.ChannelForRiser);

            lighting.Update(Rates(1, 1, 1), Midday.AddDays(2));
            int[] day2 = Snapshot(lighting.ChannelForRiser);

            lighting.Update(Rates(1, 1, 1), Midday.AddDays(3));
            int[] day3 = Snapshot(lighting.ChannelForRiser);

            // Every riser changes colour every day...
            for (int riser = 0; riser < 3; riser++)
            {
                Assert.NotEqual(day0[riser], day1[riser]);
                Assert.NotEqual(day1[riser], day2[riser]);
            }

            // ...and each riser has held all three colours exactly once over the cycle, which
            // is what lets "they prefer green" be separated from "they prefer the tube nearest
            // the food dish". Without this control the experiment is uninterpretable.
            for (int riser = 0; riser < 3; riser++)
            {
                bool[] seen = new bool[3];
                seen[day0[riser]] = true;
                seen[day1[riser]] = true;
                seen[day2[riser]] = true;

                Assert.True(seen[0] && seen[1] && seen[2]);
            }

            // Period three: day 3 is back where day 0 started.
            Assert.Equal(day0[0], day3[0]);
            Assert.Equal(day0[1], day3[1]);
            Assert.Equal(day0[2], day3[2]);
        }

        [Fact]
        public void RotationHappensAtLocalMidnightNotUtcMidnight()
        {
            LightingController lighting = new LightingController(new Setpoints(), 3);

            // 04:00 UTC is 23:00 local the previous day.
            DateTime beforeLocalMidnight = new DateTime(2026, 6, 2, 4, 0, 0);
            lighting.Update(Rates(1, 1, 1), beforeLocalMidnight);
            int[] before = Snapshot(lighting.ChannelForRiser);

            // 06:00 UTC is 01:00 local: same UTC day, next local day.
            DateTime afterLocalMidnight = new DateTime(2026, 6, 2, 6, 0, 0);
            lighting.Update(Rates(1, 1, 1), afterLocalMidnight);
            int[] after = Snapshot(lighting.ChannelForRiser);

            Assert.NotEqual(before[0], after[0]);
        }

        [Fact]
        public void OffModeIsGenuinelyDark()
        {
            LightingController lighting = new LightingController(new Setpoints(), 3);
            lighting.Mode = LightingMode.Off;

            RgbColor[] colors = lighting.Update(Rates(30, 30, 30), Midday);

            // The dark baseline has to be actually dark, or the control condition is worthless.
            foreach (RgbColor color in colors)
            {
                Assert.True(color.IsBlack);
            }
        }

        [Fact]
        public void FixedModeLightsAllRisersEquallyRegardlessOfTraffic()
        {
            LightingController lighting = new LightingController(new Setpoints(), 3);
            lighting.Mode = LightingMode.Fixed;

            RgbColor[] colors = lighting.Update(Rates(0, 15, 30), Midday);

            // Light present, carrying no information about traffic. This is what separates
            // "ants respond to colour" from "ants respond to light changing at all".
            foreach (RgbColor color in colors)
            {
                Assert.False(color.IsBlack);
            }

            double[] brightness = lighting.BrightnessFraction;
            Assert.Equal(brightness[0], brightness[1], 6);
            Assert.Equal(brightness[1], brightness[2], 6);
        }

        [Fact]
        public void ClosedLoopBrightnessTracksThatRisersOwnTraffic()
        {
            LightingController lighting = new LightingController(new Setpoints(), 3);
            lighting.Mode = LightingMode.ClosedLoop;

            // Rates kept inside full scale, or all three would clamp to 1.0 and the assertion
            // would pass or fail for the wrong reason.
            lighting.Update(Rates(0, 4, 9), Midday);
            double[] brightness = lighting.BrightnessFraction;

            Assert.Equal(0.0, brightness[0], 6);
            Assert.True(brightness[1] > brightness[0]);
            Assert.True(brightness[2] > brightness[1]);
        }

        [Fact]
        public void BrightnessIsLinearInTrafficSoTheStimulusSpansItsRange()
        {
            Setpoints setpoints = new Setpoints();
            LightingController lighting = new LightingController(setpoints, 3);
            lighting.Mode = LightingMode.ClosedLoop;

            // Half the full-scale rate must deliver half the dose.
            //
            // An earlier version gamma-corrected this on the reasoning that eyes are not linear
            // in emitted power. That is right for a display and wrong here: the audience is an
            // ant, the quantity that matters is photon dose at the choice point, and a 2.2
            // exponent left the rings at 1% brightness across all realistic traffic — the
            // experiment applied no stimulus at all. Running the simulator is what caught it.
            lighting.Update(Rates(setpoints.LightFullScaleBreaksPerMinute / 2.0, 0, 0), Midday);

            Assert.Equal(0.5, lighting.BrightnessFraction[0], 6);
        }

        [Fact]
        public void FullScaleIsAPerTubeRateNotAColonyTotal()
        {
            Setpoints setpoints = new Setpoints();

            // A Tetramorium colony at peak foraging runs on the order of ten crossings a minute
            // through its favoured tube. If full scale were set to a colony-wide figure the rings
            // would sit near dark at every realistic activity level.
            Assert.True(
                setpoints.LightFullScaleBreaksPerMinute <= 20.0,
                "full scale of " + setpoints.LightFullScaleBreaksPerMinute + "/min per tube is implausibly high");

            LightingController lighting = new LightingController(setpoints, 3);
            lighting.Mode = LightingMode.ClosedLoop;
            lighting.Update(Rates(10.0, 4.0, 1.0), Midday);

            // A busy tube at realistic traffic should be well up its range, not a rounding error.
            Assert.True(lighting.BrightnessFraction[0] > 0.5);
        }

            [Fact]
        public void TrafficAboveFullScaleSaturatesRatherThanOverflowing()
        {
            Setpoints setpoints = new Setpoints();
            LightingController lighting = new LightingController(setpoints, 3);
            lighting.Mode = LightingMode.ClosedLoop;

            RgbColor[] colors = lighting.Update(
                Rates(setpoints.LightFullScaleBreaksPerMinute * 100.0, 0, 0), Midday);

            Assert.Equal(1.0, lighting.BrightnessFraction[0], 6);

            // A byte cast on an unclamped value would have wrapped to near-black at some
            // multiple of full scale, which would look exactly like a quiet colony.
            int brightest = Math.Max(colors[0].R, Math.Max(colors[0].G, colors[0].B));
            Assert.Equal(setpoints.LightMaxBrightness, brightest);
        }

        [Fact]
        public void CurfewOverridesEveryModeSoTheColonyAlwaysGetsANight()
        {
            LightingController lighting = new LightingController(new Setpoints(), 3);
            lighting.Mode = LightingMode.ClosedLoop;

            // 04:00 UTC is 23:00 local, inside the default 22:00-07:00 curfew.
            DateTime night = new DateTime(2026, 6, 2, 4, 0, 0);

            RgbColor[] colors = lighting.Update(Rates(30, 30, 30), night);

            Assert.True(lighting.InCurfew);

            foreach (RgbColor color in colors)
            {
                Assert.True(color.IsBlack);
            }
        }

        [Fact]
        public void ChannelAssignmentStillAdvancesDuringCurfew()
        {
            LightingController lighting = new LightingController(new Setpoints(), 3);

            // The rings are dark at night but the experiment's clock must not stop, or the
            // rotation would drift relative to the logged days.
            DateTime night = new DateTime(2026, 6, 2, 4, 0, 0);
            lighting.Update(Rates(0, 0, 0), night);

            Assert.True(lighting.InCurfew);
            Assert.NotEqual(0, lighting.AssignmentDayNumber);
        }
    }
}
