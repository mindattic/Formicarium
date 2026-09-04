using System;
using Formicarium.Core.Sensing;
using Xunit;

namespace Formicarium.Controller.Tests
{
    public class TrafficCounterTests
    {
        private static readonly DateTime T0 = TestDefaults.StartUtc;

        [Fact]
        public void DebounceCollapsesTheBurstOneAntProduces()
        {
            RiserTrafficCounter counter = new RiserTrafficCounter(60, 100);

            // A single ant crossing a 3 mm beam does not produce one clean edge. The body
            // occludes for tens of milliseconds and the legs and antennae chatter the receiver
            // either side of it, so the raw signal is a burst.
            Assert.True(counter.RecordBreak(T0));
            Assert.False(counter.RecordBreak(T0.AddMilliseconds(15)));
            Assert.False(counter.RecordBreak(T0.AddMilliseconds(40)));
            Assert.False(counter.RecordBreak(T0.AddMilliseconds(80)));

            Assert.Equal(1, counter.CumulativeCount);
            Assert.Equal(3, counter.DebouncedCount);
        }

        [Fact]
        public void SeparateAntsAreCountedSeparately()
        {
            RiserTrafficCounter counter = new RiserTrafficCounter(60, 100);

            Assert.True(counter.RecordBreak(T0));
            Assert.True(counter.RecordBreak(T0.AddMilliseconds(150)));
            Assert.True(counter.RecordBreak(T0.AddMilliseconds(400)));

            Assert.Equal(3, counter.CumulativeCount);
            Assert.Equal(0, counter.DebouncedCount);
        }

        [Fact]
        public void RateReflectsTheWindow()
        {
            RiserTrafficCounter counter = new RiserTrafficCounter(60, 100);

            for (int i = 0; i < 6; i++)
            {
                counter.RecordBreak(T0.AddSeconds(i));
            }

            counter.Tick(T0.AddSeconds(6));

            // Six breaks inside a 60 second window is six per minute.
            Assert.Equal(6.0, counter.RatePerMinute, 6);
        }

        [Fact]
        public void RateDecaysToZeroWhenTheColonySleeps()
        {
            RiserTrafficCounter counter = new RiserTrafficCounter(60, 100);

            for (int i = 0; i < 6; i++)
            {
                counter.RecordBreak(T0.AddSeconds(i));
            }

            // Without ageing the window on every tick, brightness would stay frozen at whatever
            // the colony was doing when it stopped, and the lighting loop would be driven by a
            // number that had quietly stopped meaning anything.
            counter.Tick(T0.AddSeconds(120));

            Assert.Equal(0.0, counter.RatePerMinute, 6);
            Assert.Equal(6, counter.CumulativeCount);
        }

        [Fact]
        public void CumulativeCountIsMonotonicAcrossAWindowRollover()
        {
            RiserTrafficCounter counter = new RiserTrafficCounter(10, 50);

            long previous = 0;

            for (int second = 0; second < 60; second++)
            {
                counter.RecordBreak(T0.AddSeconds(second));
                Assert.True(counter.CumulativeCount >= previous);
                previous = counter.CumulativeCount;
            }

            // The dashboard differences this into a log, so it must never go backwards even as
            // the rolling window recycles its buckets underneath it.
            Assert.Equal(60, counter.CumulativeCount);
        }

        [Fact]
        public void PartialWindowExpiryDropsOnlyTheOldEntries()
        {
            RiserTrafficCounter counter = new RiserTrafficCounter(10, 50);

            counter.RecordBreak(T0);
            counter.RecordBreak(T0.AddSeconds(1));
            counter.RecordBreak(T0.AddSeconds(8));
            counter.RecordBreak(T0.AddSeconds(9));

            // At t+12 the first two have aged out of a 10 second window; the last two have not.
            counter.Tick(T0.AddSeconds(12));

            Assert.Equal(2.0 * 60.0 / 10.0, counter.RatePerMinute, 6);
        }

        [Fact]
        public void MemoryDoesNotGrowWithTraffic()
        {
            // Bounded by construction: one counter per second of window, not one entry per ant.
            // A busy colony pushing thousands of crossings an hour must not be able to grow an
            // unbounded list on a device with a few hundred kilobytes of RAM.
            RiserTrafficCounter counter = new RiserTrafficCounter(300, 1);

            for (int i = 0; i < 50000; i++)
            {
                counter.RecordBreak(T0.AddMilliseconds(i * 10));
            }

            Assert.Equal(50000, counter.CumulativeCount);
            Assert.True(counter.RatePerMinute > 0);
        }
    }
}
