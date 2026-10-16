using System;
using Formicarium.Core.Sensing;
using NUnit.Framework;

namespace Formicarium.Controller.Tests
{
    /// <summary>
    /// Reading is the type every controller decision ultimately rests on: IsUsable is the one
    /// gate that keeps a failed, stale, or out-of-range sample from ever reaching an actuator.
    /// These pin its boundaries directly.
    /// </summary>
    public class ReadingTests
    {
        private static readonly DateTime T0 = TestDefaults.StartUtc;

        [Test]
        public void DefaultReadingIsNotUsable()
        {
            Reading r = default(Reading);

            Assert.That(r.IsValid, Is.False);
            Assert.That(r.IsUsable(T0, maxAgeSeconds: int.MaxValue, plausibleMin: double.MinValue, plausibleMax: double.MaxValue), Is.False);
        }

        [Test]
        public void BadReadingIsNotUsable()
        {
            Reading r = Reading.Bad(T0);

            Assert.That(r.IsValid, Is.False);
            Assert.That(r.IsUsable(T0, maxAgeSeconds: 30, plausibleMin: -1000.0, plausibleMax: 1000.0), Is.False);
        }

        [Test]
        public void IsUsable_AgeExactlyAtMaxAgeSecondsIsStillUsable()
        {
            Reading r = Reading.Good(25.0, T0);
            DateTime now = T0.AddSeconds(30);

            Assert.That(r.IsUsable(now, maxAgeSeconds: 30, plausibleMin: -5.0, plausibleMax: 60.0), Is.True);
        }

        [Test]
        public void IsUsable_OneTickOlderThanMaxAgeSecondsIsNotUsable()
        {
            Reading r = Reading.Good(25.0, T0);
            DateTime now = T0.AddSeconds(30).AddTicks(1);

            Assert.That(r.IsUsable(now, maxAgeSeconds: 30, plausibleMin: -5.0, plausibleMax: 60.0), Is.False);
        }

        [Test]
        public void IsUsable_ValueExactlyAtPlausibleMinIsUsable()
        {
            Reading r = Reading.Good(-5.0, T0);

            Assert.That(r.IsUsable(T0, maxAgeSeconds: 30, plausibleMin: -5.0, plausibleMax: 60.0), Is.True);
        }

        [Test]
        public void IsUsable_ValueJustBelowPlausibleMinIsNotUsable()
        {
            Reading r = Reading.Good(-5.0001, T0);

            Assert.That(r.IsUsable(T0, maxAgeSeconds: 30, plausibleMin: -5.0, plausibleMax: 60.0), Is.False);
        }

        [Test]
        public void IsUsable_ValueExactlyAtPlausibleMaxIsUsable()
        {
            Reading r = Reading.Good(60.0, T0);

            Assert.That(r.IsUsable(T0, maxAgeSeconds: 30, plausibleMin: -5.0, plausibleMax: 60.0), Is.True);
        }

        [Test]
        public void IsUsable_ValueJustAbovePlausibleMaxIsNotUsable()
        {
            Reading r = Reading.Good(60.0001, T0);

            Assert.That(r.IsUsable(T0, maxAgeSeconds: 30, plausibleMin: -5.0, plausibleMax: 60.0), Is.False);
        }

        [Test]
        public void AgeSecondsIsNegativeWhenTakenUtcIsInTheFuture()
        {
            DateTime takenInFuture = T0.AddSeconds(5);
            Reading r = Reading.Good(25.0, takenInFuture);

            Assert.That(r.AgeSeconds(T0), Is.EqualTo(-5.0).Within(5e-11));
        }

        [Test]
        public void IsUsable_AFutureTakenUtcDoesNotCountAsStale()
        {
            // A clock reading one tick ahead (e.g. a sensor task that time-stamped just before
            // the control loop's own clock read) must not be treated as a missing reading.
            Reading r = Reading.Good(25.0, T0.AddSeconds(5));

            Assert.That(r.IsUsable(T0, maxAgeSeconds: 30, plausibleMin: -5.0, plausibleMax: 60.0), Is.True);
        }
    }
}
