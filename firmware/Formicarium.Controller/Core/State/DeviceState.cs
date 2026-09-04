namespace Formicarium.Core.State
{
    /// <summary>
    /// The full published state of the controller, and the contract the dashboard codes
    /// against.
    ///
    /// Deliberately flat and primitive-only. The on-device JSON serialiser is small and this
    /// is not the place to find out which nested constructs it handles, so every sensor is
    /// published as a value plus an explicit <c>...Ok</c> flag rather than as a nullable or a
    /// nested object. A consumer that ignores the Ok flag and plots the value gets a zero,
    /// which is visibly wrong, rather than a plausible stale number, which is not.
    /// </summary>
    public sealed class DeviceState
    {
        public long TimestampUnixMs { get; set; }
        public long UptimeSeconds { get; set; }

        // --- Temperatures, Celsius -------------------------------------------------------
        public double NestBottomTempC { get; set; }
        public bool NestBottomTempOk { get; set; }
        public double NestTopTempC { get; set; }
        public bool NestTopTempOk { get; set; }
        public double OutworldTempC { get; set; }
        public bool OutworldTempOk { get; set; }

        // --- Humidity, percent RH --------------------------------------------------------
        public double NestHumidityPct { get; set; }
        public bool NestHumidityOk { get; set; }
        public double OutworldHumidityPct { get; set; }
        public bool OutworldHumidityOk { get; set; }

        // --- Substrate, percent, higher is wetter ----------------------------------------
        public double SoilMoisturePct { get; set; }
        public bool SoilMoistureOk { get; set; }

        /// <summary>Float switch in the hydration reservoir.</summary>
        public bool ReservoirFull { get; set; }

        // --- Riser traffic ---------------------------------------------------------------
        /// <summary>Cumulative accepted beam breaks per riser since boot. Monotonic.</summary>
        public long[] RiserCounts { get; set; }

        /// <summary>Rolling breaks per minute per riser.</summary>
        public double[] RiserRatesPerMinute { get; set; }

        /// <summary>Edges rejected by the debounce. A high ratio means a dirty or misaligned beam.</summary>
        public long[] RiserDebouncedCounts { get; set; }

        // --- Lighting experiment ---------------------------------------------------------
        /// <summary>Channel each riser currently owns: 0 red, 1 green, 2 blue. Rotates at local midnight.</summary>
        public int[] RiserChannel { get; set; }

        /// <summary>Current stimulus per riser, 0-1. Logged alongside traffic so cause and effect can be separated.</summary>
        public double[] RiserBrightness { get; set; }

        /// <summary>Local day number the current channel mapping derives from.</summary>
        public int AssignmentDayNumber { get; set; }

        /// <summary>0 Off, 1 Fixed, 2 ClosedLoop.</summary>
        public int LightingMode { get; set; }

        public bool LightCurfewActive { get; set; }

        // --- Actuators -------------------------------------------------------------------
        public bool HeaterOn { get; set; }

        /// <summary>Filling the reservoir. The pump never reaches the nest directly.</summary>
        public bool RefillPumpOn { get; set; }

        public bool FeedPumpOn { get; set; }
        /// <summary>Outworld circulation fan.</summary>
        public bool FanOn { get; set; }

        /// <summary>Nest exhaust fan. A mould guard, gated on nest airspace humidity.</summary>
        public bool NestFanOn { get; set; }

        /// <summary>Reservoir is below full. Differs from RefillPumpOn while the cooldown holds it back.</summary>
        public bool RefillWanted { get; set; }

        // --- Health ----------------------------------------------------------------------
        /// <summary>Bitmask of <see cref="Control.Faults"/>.</summary>
        public int Faults { get; set; }

        /// <summary>All outputs held off for handling. Not a gate: nothing is closed, only stopped.</summary>
        public bool ServiceMode { get; set; }
    }
}
