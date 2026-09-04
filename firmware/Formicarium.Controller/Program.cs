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
using System.Net.NetworkInformation;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Networking;

namespace Formicarium.Controller
{
    public class Program
    {
        private const int TickIntervalMs = 1000;
        private const int NetworkWaitAttempts = 30;
        private const int NetworkWaitIntervalMs = 1000;

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

        /// <summary>
        /// Waits for an IP address and then for SNTP to set the clock.
        ///
        /// Done explicitly rather than through a helper because both halves are load-bearing and
        /// worth being able to see. The IP matters only for the dashboard; the <b>clock</b> matters
        /// to the colony, because the daily feed and the midnight colour rotation are wall-clock
        /// scheduled. A controller that came up on an epoch date would dose at the wrong hour and
        /// rotate the experiment's channel mapping against a meaningless day number.
        ///
        /// WiFi credentials are not here, and not anywhere in source: they live in the device's own
        /// configuration block, written once with <c>nanoff --updatessid</c>.
        /// </summary>
        private static bool WaitForNetwork()
        {
            try
            {
                bool haveAddress = false;

                for (int attempt = 0; attempt < NetworkWaitAttempts; attempt++)
                {
                    NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

                    for (int i = 0; i < interfaces.Length; i++)
                    {
                        string address = interfaces[i].IPv4Address;

                        if (address != null && address.Length > 0 && address != "0.0.0.0")
                        {
                            Debug.WriteLine("Network up at " + address);
                            haveAddress = true;
                            break;
                        }
                    }

                    if (haveAddress)
                    {
                        break;
                    }

                    Thread.Sleep(NetworkWaitIntervalMs);
                }

                if (!haveAddress)
                {
                    return false;
                }

                // SNTP is started by the runtime once the interface is up; this just waits for it
                // to actually land. A year still in the 1970s means it has not.
                for (int attempt = 0; attempt < NetworkWaitAttempts; attempt++)
                {
                    if (DateTime.UtcNow.Year > 2020)
                    {
                        Debug.WriteLine("Clock set: " + DateTime.UtcNow.ToString());
                        return true;
                    }

                    Thread.Sleep(NetworkWaitIntervalMs);
                }

                // An IP but no clock. Running headless is safer than running on a fake date,
                // because a wrong clock silently corrupts the feed schedule and the experiment log.
                Debug.WriteLine("Network up but clock never set - running without scheduled actions");
                return false;
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
        /// ADC1 channel for the configured soil pin. The GPIO-to-channel mapping is fixed in
        /// silicon; this table exists so the pin number stays the single source of truth in
        /// <see cref="PinMap"/> rather than being written twice.
        ///
        /// A switch statement here would be compiled away, since the pin is a const - the compiler
        /// resolves it and warns that every other arm is unreachable. A lookup keeps the whole
        /// mapping visible and reviewable against the datasheet.
        /// </summary>
        private static int SoilAdcChannel()
        {
            int[] adc1Pins = new int[] { 36, 37, 38, 39, 32, 33, 34, 35 };

            for (int channel = 0; channel < adc1Pins.Length; channel++)
            {
                if (adc1Pins[channel] == PinMap.SoilMoistureAdc)
                {
                    return channel;
                }
            }

            // Not an ADC1 pin at all. ADC2 is unusable while WiFi is active, so this is a wiring
            // design error rather than a runtime condition worth recovering from.
            throw new ArgumentException("Soil pin GPIO" + PinMap.SoilMoistureAdc + " is not on ADC1");
        }
    }
}
