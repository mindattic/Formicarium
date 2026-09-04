namespace Formicarium.Core.Config
{
    /// <summary>
    /// Every GPIO the controller touches.
    ///
    /// This must stay in lockstep with <c>docs/gpio-map.md</c> and the pin-out diagram in
    /// <c>docs/build-guide.html</c>. A silent disagreement between the three is the most
    /// likely latent bug in this repository, so the constraint comments below are duplicated
    /// deliberately rather than left in the docs alone.
    /// </summary>
    public static class PinMap
    {
        /// <summary>Number of bypass risers connecting Colony to Outworld.</summary>
        public const int RiserCount = 3;

        // --- I2C: both SHT31 sensors share one bus ---------------------------------------
        // Addresses 0x44 (outworld) and 0x45 (nest), selected by the ADDR pin.
        public const int I2cSda = 21;
        public const int I2cScl = 22;
        public const byte Sht31OutworldAddress = 0x44;
        public const byte Sht31NestAddress = 0x45;

        // --- 1-Wire: all three DS18B20 probes share one bus ------------------------------
        // nanoFramework drives 1-Wire over a UART, not by bit-banging, so this needs BOTH a
        // RX and a TX pin tied together to the DQ line, with a 4.7k pull-up to 3V3. Without
        // the pull-up the bus reads all-ones and every probe enumerates as absent.
        public const int OneWireRx = 16; // COM3_RX
        public const int OneWireTx = 17; // COM3_TX

        // --- Analog ----------------------------------------------------------------------
        // MUST be an ADC1 pin (32-36, 39). ADC2 is unavailable whenever WiFi is active, which
        // is the single most common ESP32 sensor bug. GPIO34 is also input-only, which is
        // correct for an analog input.
        public const int SoilMoistureAdc = 34;

        // --- IR break beams: one per riser -----------------------------------------------
        // Open-collector receivers, so these need pull-ups. All three pins have internal
        // pull-ups available; the input-only pins (34-39) do not, which is why none are used
        // here.
        public static readonly int[] BeamReceivers = new int[] { 27, 14, 13 };

        /// <summary>Powers all three emitters together.</summary>
        public const int BeamEmitterEnable = 19;

        // --- Actuators: all low-side IRLZ44N, 150R gate, 10k pulldown --------------------
        // The pulldowns matter: they hold every output off through boot and reset, before
        // firmware has run at all.
        public const int Heater = 25;      // 12V DC cable wrapped outside the Colony section
        public const int RefillPump = 26;  // water -> hydration reservoir, never the nest
        public const int FeedPump = 4;     // sugar water -> outworld dish
        public const int Fan = 33;         // outworld circulation

        /// <summary>
        /// Nest exhaust fan. Sits in the sealed electronics bay and draws nest air up through a
        /// mesh-screened port in the nest ceiling, with a passive mesh intake low on the nest
        /// wall giving bottom-to-top cross-flow along the thermal gradient.
        ///
        /// The nest is never opened once the colony is in, so ventilation had to be something
        /// serviceable entirely from outside it. Everything mechanical here is on the electronics
        /// blade; the only thing inside the nest is stainless mesh, which has nothing to fail.
        /// </summary>
        public const int NestFan = 18;

        // --- Reservoir level ------------------------------------------------------------
        // Float switch wired to ground and read against the internal pull-up, so LOW means the
        // float has risen and the reservoir is full.
        //
        // That polarity is chosen deliberately: a broken wire reads HIGH, which is "not full",
        // which asks for a refill that both the runtime limiter and the reservoir's own size
        // already bound. The opposite polarity would read "full" forever and silently stop
        // hydrating the colony, which is the failure nobody notices.
        public const int ReservoirLevel = 32;

        // --- WS2812B riser-mouth rings ---------------------------------------------------
        // All three rings chained on a single data line, driven over the ESP32's RMT
        // peripheral. They sit inside the sealed electronics bay and shine up through frosted
        // ring-windows in the outworld floor: no ant contact, no moisture contact.
        public const int RiserLedData = 23;
        public const int LedsPerRing = 12;

        // --- Status ----------------------------------------------------------------------
        public const int StatusLed = 2;
    }
}
