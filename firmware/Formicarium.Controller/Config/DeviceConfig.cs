namespace Formicarium.Controller.Config
{
    /// <summary>
    /// Per-unit values that can only be discovered by having the hardware in front of you.
    /// Everything here is filled in during bring-up, not guessed in advance.
    ///
    /// This file deliberately contains no WiFi credentials. nanoFramework stores those in the
    /// device's own configuration block, written once with
    /// <c>nanoff --updatessid</c>, so they never enter source control and survive a reflash.
    /// </summary>
    public static class DeviceConfig
    {
        /// <summary>
        /// 64-bit ROM IDs of the three DS18B20 probes.
        ///
        /// These MUST be recorded explicitly rather than assigned by bus enumeration order.
        /// Enumeration order is stable in practice but not guaranteed, and the failure mode of
        /// getting it wrong is silent and slow: the controller heats to the outworld probe and
        /// cooks the nest, or heats to the nest-top probe and never reaches setpoint. Neither
        /// looks like a wiring fault.
        ///
        /// Bring-up procedure: flash with these left null, watch the boot log print every
        /// discovered ID, then warm one probe at a time in a closed hand to identify which is
        /// which, and paste them back in here.
        /// </summary>
        public static readonly byte[] NestBottomProbeRom = null;

        public static readonly byte[] NestTopProbeRom = null;

        public static readonly byte[] OutworldProbeRom = null;

        /// <summary>
        /// Raw ADC counts with the capacitive probe held in open air.
        ///
        /// Capacitive soil probes read HIGHER when DRIER, which is the opposite of the
        /// intuitive direction and the source of a great many inverted-control bugs. The
        /// inversion is applied once, in the sensor adapter, so nothing above it has to
        /// remember this.
        /// </summary>
        public const int SoilCountsDry = 2800;

        /// <summary>Raw ADC counts with the probe buried in saturated substrate.</summary>
        public const int SoilCountsWet = 1300;
    }
}
