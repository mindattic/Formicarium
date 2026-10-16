using System;
using Formicarium.Core.Control;
using Formicarium.Core.Sensing;
using NUnit.Framework;

namespace Formicarium.Controller.Tests
{
    /// <summary>
    /// The heater is the actuator that can kill the colony fastest, so its fail-safe behaviour
    /// is pinned harder than anything else here.
    /// </summary>
    public class HeatingTests
    {
        private static readonly DateTime T0 = TestDefaults.StartUtc;

        private static HysteresisController Heater()
        {
            // On below 26.1 C (79 F), off above 28.3 C (83 F), 60 s dwell either side,
            // readings older than 30 s or outside -5..60 C are not readings at all.
            return new HysteresisController(
                HysteresisMode.ActivateBelow, 26.1, 28.3, -5.0, 60.0, 60, 60, 30);
        }

        [Test]
        public void TurnsOnBelowTheBand()
        {
            HysteresisController heater = Heater();

            Assert.That(heater.Update(Reading.Good(24.0, T0), T0), Is.True);
        }

        [Test]
        public void TurnsOffAboveTheBandOnceDwellHasElapsed()
        {
            HysteresisController heater = Heater();
            heater.Update(Reading.Good(24.0, T0), T0);

            DateTime later = T0.AddSeconds(61);

            Assert.That(heater.Update(Reading.Good(29.0, later), later), Is.False);
        }

        [Test]
        public void HoldsStateInsideTheDeadband()
        {
            HysteresisController heater = Heater();
            heater.Update(Reading.Good(24.0, T0), T0);

            DateTime later = T0.AddSeconds(120);

            // 27.0 is between the two thresholds. Neither rule fires, so whatever the heater
            // was doing continues. This is the whole point of a deadband.
            Assert.That(heater.Update(Reading.Good(27.0, later), later), Is.True);
        }

        [Test]
        public void DwellBlocksChatterAtTheSetpointEdge()
        {
            HysteresisController heater = Heater();
            heater.Update(Reading.Good(24.0, T0), T0);

            // Sensor noise flicks the reading across the upper threshold almost immediately.
            DateTime tooSoon = T0.AddSeconds(10);
            Assert.That(heater.Update(Reading.Good(29.0, tooSoon), tooSoon), Is.True);

            DateTime allowed = T0.AddSeconds(61);
            Assert.That(heater.Update(Reading.Good(29.0, allowed), allowed), Is.False);
        }

        [Test]
        public void FailedReadingTurnsHeaterOffImmediatelyAndIgnoresDwell()
        {
            HysteresisController heater = Heater();
            heater.Update(Reading.Good(24.0, T0), T0);

            // One second in, far inside the minimum-on dwell. The dwell must not delay a
            // retreat to the safe state: a heater held on because "it has not been on long
            // enough to turn off" is precisely the bug this ordering exists to prevent.
            DateTime justAfter = T0.AddSeconds(1);

            Assert.That(heater.Update(Reading.Bad(justAfter), justAfter), Is.False);
        }

        [Test]
        public void NeverHoldsTheLastGoodValueWhenTheSensorStops()
        {
            HysteresisController heater = Heater();
            heater.Update(Reading.Good(24.0, T0), T0);

            // A reading that was perfectly good 40 seconds ago, re-presented. It is stale past
            // the 30 s limit, so it must be treated as no reading at all rather than as
            // evidence the nest is still cold.
            Reading stale = Reading.Good(24.0, T0);
            DateTime now = T0.AddSeconds(40);

            Assert.That(heater.Update(stale, now), Is.False);
        }

        [Test]
        public void RejectsTheDs18B20PowerOnDefaultAsImplausible()
        {
            HysteresisController heater = Heater();
            heater.Update(Reading.Good(24.0, T0), T0);

            // 85.0 C is what a DS18B20 reports when it has been addressed but never completed a
            // conversion, which is what a marginal connection looks like. It arrives as a
            // perfectly well-formed reading, so validity alone would not catch it: only the
            // plausibility bound does.
            DateTime later = T0.AddSeconds(61);

            Assert.That(heater.Update(Reading.Good(85.0, later), later), Is.False);
        }

        [Test]
        public void FanModeIsTheSameControlReversed()
        {
            // On above 80 %RH, off below 70 %RH.
            HysteresisController fan = new HysteresisController(
                HysteresisMode.ActivateAbove, 80.0, 70.0, 0.0, 100.0, 30, 30, 30);

            Assert.That(fan.Update(Reading.Good(85.0, T0), T0), Is.True);

            DateTime hold = T0.AddSeconds(31);
            Assert.That(fan.Update(Reading.Good(75.0, hold), hold), Is.True); // deadband

            DateTime off = T0.AddSeconds(62);
            Assert.That(fan.Update(Reading.Good(65.0, off), off), Is.False);
        }
    }
}
