using System;
using Formicarium.Core.Config;
using Formicarium.Core.Control;
using NUnit.Framework;

namespace Formicarium.Controller.Tests
{
    public class FeedingTests
    {
        // Setpoints default to a -300 minute offset and a 09:00 local feed, so the scheduled
        // dose lands at 14:00 UTC.
        private static readonly DateTime FeedTimeUtc = new DateTime(2026, 6, 1, 14, 0, 0);

        [Test]
        public void ARebootAfterTheFeedHourDoesNotTriggerAnExtraDose()
        {
            FeedScheduler feed = new FeedScheduler(new Setpoints());

            // Controller comes up at 11:00 local, two hours after the slot. Without the
            // first-pass guard this dosed on every startup, so a brownout loop or a watchdog
            // reset would empty the syrup reservoir into the dish.
            DateTime bootUtc = FeedTimeUtc.AddHours(2);

            Assert.That(feed.Update(bootUtc), Is.False);
            Assert.That(feed.IsDosing, Is.False);
        }

        [Test]
        public void DosesWhenTheScheduledTimeArrives()
        {
            FeedScheduler feed = new FeedScheduler(new Setpoints());

            DateTime beforeSlot = FeedTimeUtc.AddHours(-2);
            Assert.That(feed.Update(beforeSlot), Is.False);

            Assert.That(feed.Update(FeedTimeUtc), Is.True);
            Assert.That(feed.IsDosing, Is.True);
        }

        [Test]
        public void DoseStopsAfterTheConfiguredDuration()
        {
            Setpoints setpoints = new Setpoints();
            FeedScheduler feed = new FeedScheduler(setpoints);

            feed.Update(FeedTimeUtc.AddHours(-2));
            feed.Update(FeedTimeUtc);

            DateTime midDose = FeedTimeUtc.AddSeconds(setpoints.FeedDoseSeconds - 1);
            Assert.That(feed.Update(midDose), Is.True);

            DateTime afterDose = FeedTimeUtc.AddSeconds(setpoints.FeedDoseSeconds);
            Assert.That(feed.Update(afterDose), Is.False);
        }

        [Test]
        public void DosesAtMostOncePerLocalDay()
        {
            Setpoints setpoints = new Setpoints();
            FeedScheduler feed = new FeedScheduler(setpoints);

            feed.Update(FeedTimeUtc.AddHours(-2));
            feed.Update(FeedTimeUtc);
            feed.Update(FeedTimeUtc.AddSeconds(setpoints.FeedDoseSeconds));

            // Still the same local day, well past the slot.
            Assert.That(feed.Update(FeedTimeUtc.AddHours(6)), Is.False);

            // Next local day, same slot.
            Assert.That(feed.Update(FeedTimeUtc.AddDays(1)), Is.True);
        }

        [Test]
        public void ManualDoseIsRateLimited()
        {
            Setpoints setpoints = new Setpoints();
            FeedScheduler feed = new FeedScheduler(setpoints);

            DateTime t = FeedTimeUtc.AddHours(3);

            Assert.That(feed.RequestManualDose(t), Is.True);
            feed.Update(t.AddSeconds(setpoints.FeedDoseSeconds));

            // The dish is small; an over-eager button is a drowned outworld.
            Assert.That(feed.RequestManualDose(t.AddMinutes(5)), Is.False);

            Assert.That(feed.RequestManualDose(t.AddSeconds(setpoints.FeedManualCooldownSeconds + 1)), Is.True);
        }

        [Test]
        public void ManualDoseIsRefusedWhileAlreadyDosing()
        {
            FeedScheduler feed = new FeedScheduler(new Setpoints());

            DateTime t = FeedTimeUtc.AddHours(3);
            Assert.That(feed.RequestManualDose(t), Is.True);
            Assert.That(feed.RequestManualDose(t.AddSeconds(1)), Is.False);
        }
    }
}
