using System;

namespace Formicarium.Core.Sensing
{
    /// <summary>
    /// One pass of every sensor, taken together so that all control decisions in a single tick
    /// are made against a consistent view rather than against values read at slightly
    /// different moments.
    /// </summary>
    public sealed class SensorSnapshot
    {
        public SensorSnapshot(int riserCount)
        {
            RiserCounts = new long[riserCount];
            RiserRatesPerMinute = new double[riserCount];
            RiserDebouncedCounts = new long[riserCount];
        }

        /// <summary>Bottom of the substrate column, nearest the heat cable. This is the control probe.</summary>
        public Reading NestBottomTempC { get; set; }

        /// <summary>Top of the substrate column. Reported, never controlled to: the gap between this and the bottom IS the gradient.</summary>
        public Reading NestTopTempC { get; set; }

        public Reading OutworldTempC { get; set; }

        /// <summary>SHT31-B, 0x45. Nest airspace, where mould risk lives.</summary>
        public Reading NestHumidityPct { get; set; }

        /// <summary>SHT31-A, 0x44. Outworld air, which is what the fan can actually act on.</summary>
        public Reading OutworldHumidityPct { get; set; }

        /// <summary>Normalised 0-100, higher meaning wetter. The adapter does the inversion and calibration.</summary>
        public Reading SoilMoisturePct { get; set; }

        /// <summary>
        /// Float switch in the hydration reservoir. A plain bool rather than a Reading, because a
        /// float switch has no way to report its own failure - a stuck one is caught behaviourally
        /// instead, by a refill that times out without the level ever reading full.
        ///
        /// Defaults to false, meaning "not full", which is the safe default: it asks for a refill
        /// that is bounded twice over rather than silently stopping hydration.
        /// </summary>
        public bool ReservoirFull { get; set; }

        /// <summary>Cumulative beam breaks per riser since boot. Monotonic, so the dashboard can difference them.</summary>
        public long[] RiserCounts { get; private set; }

        /// <summary>Rolling traffic rate per riser. This is what drives the lighting loop.</summary>
        public double[] RiserRatesPerMinute { get; private set; }

        /// <summary>Edges rejected by the debounce, per riser. A high ratio means a dirty or misaligned beam.</summary>
        public long[] RiserDebouncedCounts { get; private set; }
    }

    /// <summary>Reads every sensor. The only interface the control loop needs to get data.</summary>
    public interface ISensorSet
    {
        SensorSnapshot Poll(DateTime nowUtc);
    }
}
