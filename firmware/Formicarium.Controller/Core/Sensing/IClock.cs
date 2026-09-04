using System;

namespace Formicarium.Core.Sensing
{
    /// <summary>
    /// Time, injected rather than read from a static, so every timing rule in this firmware
    /// (hysteresis dwell, mist cooldown, feed schedule, midnight colour rotation) can be
    /// tested in milliseconds instead of hours.
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    /// <summary>The real clock. Used on device; never used in tests.</summary>
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }
    }

    /// <summary>
    /// Local-time helpers. The ESP32 has no timezone database, so "local" here means UTC plus
    /// a fixed configured offset. That is sufficient for the two things that need it — the
    /// daily feed and the midnight colour rotation — and it avoids pretending to handle DST
    /// transitions that this device has no way to know about.
    /// </summary>
    public static class LocalTime
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

        public static DateTime FromUtc(DateTime utcNow, int utcOffsetMinutes)
        {
            return utcNow.AddMinutes(utcOffsetMinutes);
        }

        /// <summary>
        /// Whole days since 1970-01-01 in local time. Used as the key for the daily colour
        /// rotation so the riser-to-channel mapping is reproducible from a timestamp alone
        /// during later analysis, rather than depending on controller uptime.
        /// </summary>
        public static int DayNumber(DateTime utcNow, int utcOffsetMinutes)
        {
            DateTime local = FromUtc(utcNow, utcOffsetMinutes);
            return (int)((local.Date - Epoch).TotalDays);
        }
    }
}
