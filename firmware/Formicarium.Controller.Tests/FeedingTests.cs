using System;
using Formicarium.Core.Config;
using Formicarium.Core.Control;
using Xunit;

namespace Formicarium.Controller.Tests
{
    public class FeedingTests
    {
        // Setpoints default to a -300 minute offset and a 09:00 local feed, so the scheduled
        // dose lands at 14:00 UTC.
        private static readonly DateTime FeedTimeUtc = new DateTime(2026, 6, 1, 14, 0, 0);

        [Fact]
        public void ARebootAfterTheFeedHourDoesNotTriggerAnExtraDose()
        {
            FeedScheduler feed = new FeedScheduler(new Setpoints());

            // Controller comes up at 11:00 local, two hours after the slot. Without the
            // first-pass guard this dosed on every startup, so a brownout loop or a watchdog
            // reset would empty the syrup reservoir into the dish.
            DateTime bootUtc = FeedTimeUtc.AddHours(2);

            Assert.False(feed.Update(bootUtc));
            Assert.False(feed.IsDosing);
        }

        [Fact]
        public void DosesWhenTheScheduledTimeArrives()
        {
            FeedScheduler feed = new FeedScheduler(new Setpoints());

            DateTime beforeSlot = FeedTimeUtc.AddHours(-2);
            Assert.False(feed.Update(beforeSlot));

            Assert.True(feed.Update(FeedTimeUtc));
            Assert.True(feed.IsDosing);
        }

        [Fact]
        public void DoseStopsAfterTheConfiguredDuration()
        {
            Setpoints setpoints = new Setpoints();
            FeedScheduler feed = new FeedScheduler(setpoints);

            feed.Update(FeedTimeUtc.AddHours(-2));
            feed.Update(FeedTimeUtc);

            DateTime midDose = FeedTimeUtc.AddSeconds(setpoints.FeedDoseSeconds - 1);
            Assert.True(feed.Update(midDose));

            DateTime afterDose = FeedTimeUtc.AddSeconds(setpoints.FeedDoseSeconds);
            Assert.False(feed.Update(afterDose));
        }

        [Fact]
        public void DosesAtMostOncePerLocalDay()
        {
            Setpoints setpoints = new Setpoints();
            FeedScheduler feed = new FeedScheduler(setpoints);

            feed.Update(FeedTimeUtc.AddHours(-2));
            feed.Update(FeedTimeUtc);
            feed.Update(FeedTimeUtc.AddSeconds(setpoints.FeedDoseSeconds));

            // Still the same local day, well past the slot.
            Assert.False(feed.Update(FeedTimeUtc.AddHours(6)));

            // Next local day, same slot.
            Assert.True(feed.Update(FeedTimeUtc.AddDays(1)));
        }

        [Fact]
        public void ManualDoseIsRateLimited()
        {
            Setpoints setpoints = new Setpoints();
            FeedScheduler feed = new FeedScheduler(setpoints);

            DateTime t = FeedTimeUtc.AddHours(3);

            Assert.True(feed.RequestManualDose(t));
            feed.Update(t.AddSeconds(setpoints.FeedDoseSeconds));

            // The dish is small; an over-eager button is a drowned outworld.
            Assert.False(feed.RequestManualDose(t.AddMinutes(5)));

            Assert.True(feed.RequestManualDose(t.AddSeconds(setpoints.FeedManualCooldownSeconds + 1)));
        }

        [Fact]
        public void ManualDoseIsRefusedWhileAlreadyDosing()
        {
            FeedScheduler feed = new FeedScheduler(new Setpoints());

            DateTime t = FeedTimeUtc.AddHours(3);
            Assert.True(feed.RequestManualDose(t));
            Assert.False(feed.RequestManualDose(t.AddSeconds(1)));
        }
    }
}
