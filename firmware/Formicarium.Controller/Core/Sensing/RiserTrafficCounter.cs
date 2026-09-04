using System;

namespace Formicarium.Core.Sensing
{
    /// <summary>
    /// Debounced beam-break counting and a rolling traffic rate for one riser.
    ///
    /// Two things make raw edges unusable on their own. A single ant occludes a 3 mm beam for
    /// only tens of milliseconds, and its legs and antennae chatter the receiver either side
    /// of the body, so one ant produces a burst of edges. And the lighting loop needs a
    /// <i>rate</i>, not a total, because brightness has to track current activity rather than
    /// grow monotonically forever.
    ///
    /// Memory is bounded and does not depend on traffic volume: one counter per second of
    /// window, with a running sum, rather than a list of timestamps that a busy colony could
    /// grow without limit.
    /// </summary>
    public sealed class RiserTrafficCounter
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

        private readonly int[] _buckets;
        private readonly int _windowSeconds;
        private readonly int _debounceMs;

        private int _runningSum;
        private long _currentSecond;
        private int _currentIndex;
        private bool _started;

        private DateTime _lastAcceptedUtc;
        private bool _hasAccepted;

        public RiserTrafficCounter(int windowSeconds, int debounceMs)
        {
            if (windowSeconds < 1)
            {
                windowSeconds = 1;
            }

            _windowSeconds = windowSeconds;
            _debounceMs = debounceMs;
            _buckets = new int[windowSeconds];
        }

        /// <summary>Total accepted breaks since boot. Monotonic, so the dashboard can difference it into a log.</summary>
        public long CumulativeCount { get; private set; }

        /// <summary>Breaks rejected by the debounce. Surfaced because a high ratio means a dirty or misaligned beam.</summary>
        public long DebouncedCount { get; private set; }

        public double RatePerMinute
        {
            get { return _runningSum * 60.0 / _windowSeconds; }
        }

        /// <summary>
        /// Record one beam break. Called from the hardware interrupt path on device, and
        /// directly in tests. Returns false if the edge was swallowed by the debounce.
        /// </summary>
        public bool RecordBreak(DateTime nowUtc)
        {
            if (_hasAccepted && (nowUtc - _lastAcceptedUtc).TotalMilliseconds < _debounceMs)
            {
                DebouncedCount++;
                return false;
            }

            Advance(nowUtc);

            _lastAcceptedUtc = nowUtc;
            _hasAccepted = true;
            CumulativeCount++;
            _buckets[_currentIndex]++;
            _runningSum++;
            return true;
        }

        /// <summary>
        /// Age the window forward. Must be called every tick even when nothing is happening,
        /// otherwise the rate would stay frozen at its last value while the colony sleeps.
        /// </summary>
        public void Tick(DateTime nowUtc)
        {
            Advance(nowUtc);
        }

        private void Advance(DateTime nowUtc)
        {
            long second = (long)((nowUtc - Epoch).TotalSeconds);

            if (!_started)
            {
                _started = true;
                _currentSecond = second;
                _currentIndex = 0;
                return;
            }

            long elapsed = second - _currentSecond;

            if (elapsed <= 0)
            {
                return;
            }

            // A gap longer than the window means everything in it has expired. Clearing the
            // whole array is both correct and cheaper than stepping through it.
            if (elapsed >= _windowSeconds)
            {
                for (int i = 0; i < _windowSeconds; i++)
                {
                    _buckets[i] = 0;
                }

                _runningSum = 0;
                _currentIndex = 0;
                _currentSecond = second;
                return;
            }

            for (long i = 0; i < elapsed; i++)
            {
                _currentIndex = (_currentIndex + 1) % _windowSeconds;
                _runningSum -= _buckets[_currentIndex];
                _buckets[_currentIndex] = 0;
            }

            _currentSecond = second;
        }
    }
}
