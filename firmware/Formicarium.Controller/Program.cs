using System;
using System.Collections;
using System.Device.Adc;
using System.Device.Gpio;
using System.Device.I2c;
using System.Diagnostics;
using System.Threading;
using Formicarium.Controller.Config;
using Formicarium.Controller.Hardware;
using Formicarium.Controller.Net;
using Formicarium.Core.Config;
using Formicarium.Core.Control;
using Formicarium.Core.State;
using nanoFramework.Device.OneWire;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Networking;

namespace Formicarium.Controller
{
    public class Program
    {
        private const int TickIntervalMs = 1000;

        public static void Main()
        {
            Debug.WriteLine("Formicarium controller 1.0.0 starting");

            ConfigurePinFunctions();

            // The feed schedule and the midnight colour rotation both need real wall-clock time,
            // not just uptime, so the controller waits for SNTP rather than starting on an epoch
            // date and quietly dosing at the wrong hour for a week.
            bool networkUp = WaitForNetwork();

            GpioController gpio = new GpioController();

            OneWireHost oneWire = new OneWireHost();
            LogDiscoveredProbes(oneWire);

            Setpoints setpoints = new Setpoints();

            I2cDevice outworldSht = I2cDevice.Create(
                new I2cConnectionSettings(1, PinMap.Sht31OutworldAddress, I2cBusSpeed.StandardMode));

            I2cDevice nestSht = I2cDevice.Create(
                new I2cConnectionSettings(1, PinMap.Sht31NestAddress, I2cBusSpeed.StandardMode));

            AdcController adc = new AdcController();
            AdcChannel soil = adc.OpenChannel(SoilAdcChannel());

            Esp32SensorSet sensors = new Esp32SensorSet(
                gpio,
                oneWire,
                outworldSht,
                nestSht,
                soil,
                DeviceConfig.NestBottomProbeRom,
                DeviceConfig.NestTopProbeRom,
                DeviceConfig.OutworldProbeRom,
                DeviceConfig.SoilCountsDry,
                DeviceConfig.SoilCountsWet,
                setpoints);

            Esp32ActuatorSet actuators = new Esp32ActuatorSet(gpio, PinMap.RiserCount);

            ControlLoop loop = new ControlLoop(
                sensors, actuators, setpoints, DateTime.UtcNow, PinMap.RiserCount);

            HttpApiServer server = new HttpApiServer(loop);

            if (networkUp)
            {
                server.Start();
                Debug.WriteLine("HTTP API listening on port 80");
            }
            else
            {
                // Losing the network must not stop the colony being kept alive. The control loop
                // runs regardless; only remote visibility is lost.
                Debug.WriteLine("No network. Running headless; control loop unaffected.");
            }

            bool heartbeat = false;

            while (true)
            {
                try
                {
                    DeviceState state = loop.Tick(DateTime.UtcNow);
                    server.Publish(state);

                    heartbeat = !heartbeat;
                    actuators.SetStatusLed(heartbeat);
                }
                catch (Exception ex)
                {
                    // Nothing in the loop is expected to throw — every sensor read is already
                    // wrapped — but if something does, the only safe response is to drop every
                    // output and keep trying rather than to die holding the heater on.
                    Debug.WriteLine("Tick failed: " + ex.Message);
                    actuators.AllOff();
                }

                Thread.Sleep(TickIntervalMs);
            }
        }

        private static void ConfigurePinFunctions()
        {
            // On ESP32 the peripheral-to-pin mapping is set in software, so these calls must
            // happen before any bus is opened.
            Configuration.SetPinFunction(PinMap.I2cSda, DeviceFunction.I2C1_DATA);
            Configuration.SetPinFunction(PinMap.I2cScl, DeviceFunction.I2C1_CLOCK);

            // 1-Wire is UART-backed here, not bit-banged: it needs both a RX and a TX pin, wired
            // together to the DQ line with a 4.7k pull-up to 3V3. This is the single easiest
            // detail in the whole build to get wrong, and it fails as "no probes found" rather
            // than as an error.
            Configuration.SetPinFunction(PinMap.OneWireRx, DeviceFunction.COM3_RX);
            Configuration.SetPinFunction(PinMap.OneWireTx, DeviceFunction.COM3_TX);
        }

        private static bool WaitForNetwork()
        {
            try
            {
                // Credentials come from the device's stored configuration, written once with
                // nanoff --updatessid. requiresDateTime waits for SNTP as well as an IP.
                return WifiNetworkHelper.Reconnect(requiresDateTime: true, token: new CancellationTokenSource(60000).Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Network setup failed: " + ex.Message);
                return false;
            }
        }

        private static void LogDiscoveredProbes(OneWireHost oneWire)
        {
            try
            {
                ArrayList devices = oneWire.FindAllDevices();

                Debug.WriteLine("1-Wire devices found: " + devices.Count);

                foreach (byte[] rom in devices)
                {
                    Debug.WriteLine("  " + BytesToHex(rom));
                }

                if (devices.Count == 0)
                {
                    Debug.WriteLine("  none - check the 4.7k pull-up and that RX/TX are tied to DQ");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("1-Wire enumeration failed: " + ex.Message);
            }
        }

        private static string BytesToHex(byte[] bytes)
        {
            string result = string.Empty;

            for (int i = 0; i < bytes.Length; i++)
            {
                result += bytes[i].ToString("X2");

                if (i < bytes.Length - 1)
                {
                    result += "-";
                }
            }

            return result;
        }

        /// <summary>
        /// GPIO34 is ADC1 channel 6. The mapping is fixed in silicon; this exists so the pin
        /// number stays the single source of truth in <see cref="PinMap"/>.
        /// </summary>
        private static int SoilAdcChannel()
        {
            switch (PinMap.SoilMoistureAdc)
            {
                case 36: return 0;
                case 37: return 1;
                case 38: return 2;
                case 39: return 3;
                case 32: return 4;
                case 33: return 5;
                case 34: return 6;
                case 35: return 7;
                default: return 6;
            }
        }
    }
}
