using System;
using Formicarium.Core.Sensing;
using NUnit.Framework;

namespace Formicarium.Controller.Tests
{
    /// <summary>
    /// LocalTime.DayNumber is the key for the daily riser colour rotation, and FromUtc gates
    /// the daily feed and light curfew. Both depend entirely on UtcOffsetMinutes arithmetic
    /// rolling correctly across a local midnight, so these pin that directly rather than
    /// relying on it being exercised incidentally through FeedingTests/LightingTests.
    /// </summary>
    public class LocalTimeTests
    {
        [Test]
        public void FromUtc_DefaultOffsetRollsAnEarlyMorningUtcTimestampBackToThePreviousLocalDay()
        {
            // -300 minutes is the configured default (US Eastern, standard time).
            DateTime utc = new DateTime(2026, 6, 2, 3, 0, 0);

            DateTime local = LocalTime.FromUtc(utc, -300);

            Assert.That(local, Is.EqualTo(new DateTime(2026, 6, 1, 22, 0, 0)));
        }

        [Test]
        public void FromUtc_PositiveOffsetCanAdvanceIntoTheNextLocalDay()
        {
            DateTime utc = new DateTime(2026, 6, 1, 23, 30, 0);

            DateTime local = LocalTime.FromUtc(utc, 60);

            Assert.That(local, Is.EqualTo(new DateTime(2026, 6, 2, 0, 30, 0)));
        }

        [Test]
        public void FromUtc_ZeroOffsetIsIdentity()
        {
            DateTime utc = new DateTime(2026, 6, 1, 16, 0, 0);

            DateTime local = LocalTime.FromUtc(utc, 0);

            Assert.That(local, Is.EqualTo(utc));
        }

        [Test]
        public void DayNumberOfTheEpochIsZero()
        {
            DateTime epoch = new DateTime(1970, 1, 1);

            Assert.That(LocalTime.DayNumber(epoch, 0), Is.EqualTo(0));
        }

        [Test]
        public void DayNumber_DefaultOffsetRollsAnEarlyMorningUtcTimestampBackToThePreviousLocalDay()
        {
            // 03:00 UTC on the 2nd is still the 1st at the default -300 offset (22:00 local).
            DateTime utc = new DateTime(2026, 6, 2, 3, 0, 0);

            int utcDayNumber = LocalTime.DayNumber(utc, 0);
            int localDayNumber = LocalTime.DayNumber(utc, -300);

            Assert.That(localDayNumber, Is.EqualTo(utcDayNumber - 1));
        }

        [Test]
        public void DayNumber_PositiveOffsetCanAdvanceToTheNextLocalDay()
        {
            DateTime utc = new DateTime(2026, 6, 1, 23, 30, 0);

            int utcDayNumber = LocalTime.DayNumber(utc, 0);
            int localDayNumber = LocalTime.DayNumber(utc, 60);

            Assert.That(localDayNumber, Is.EqualTo(utcDayNumber + 1));
        }

        [Test]
        public void DayNumber_IsStableAcrossTheWholeLocalDayRegardlessOfTimeOfDay()
        {
            DateTime earlyLocal = new DateTime(2026, 6, 2, 3, 0, 1);  // just after local midnight
            DateTime lateLocal = new DateTime(2026, 6, 2, 23, 59, 59); // just before local midnight

            // Convert local wall-clock moments on the same day back to the UTC instants that
            // would produce them at the default offset.
            DateTime earlyUtc = earlyLocal.AddMinutes(300);
            DateTime lateUtc = lateLocal.AddMinutes(300);

            Assert.That(LocalTime.DayNumber(lateUtc, -300), Is.EqualTo(LocalTime.DayNumber(earlyUtc, -300)));
        }
    }
}
