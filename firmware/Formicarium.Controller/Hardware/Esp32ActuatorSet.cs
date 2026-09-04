using System.Device.Gpio;
using Formicarium.Core.Actuation;
using Formicarium.Core.Config;
using Iot.Device.Ws28xx.Esp32;

namespace Formicarium.Controller.Hardware
{
    /// <summary>
    /// The only class in this firmware that knows what a GPIO is.
    ///
    /// Everything above it decides <i>what</i> to switch; this decides <i>how</i>. Keeping that
    /// line sharp is what lets the entire control policy run under a desktop test runner.
    /// </summary>
    public sealed class Esp32ActuatorSet : IActuatorSet
    {
        private readonly GpioPin _heater;
        private readonly GpioPin _refillPump;
        private readonly GpioPin _feedPump;
        private readonly GpioPin _fan;
        private readonly GpioPin _nestFan;
        private readonly GpioPin _statusLed;
        private readonly Ws2812b _rings;
        private readonly int _riserCount;
        private readonly int _ledsPerRing;

        public Esp32ActuatorSet(GpioController gpio, int riserCount)
        {
            _riserCount = riserCount;
            _ledsPerRing = PinMap.LedsPerRing;

            _heater = gpio.OpenPin(PinMap.Heater, PinMode.Output);
            _refillPump = gpio.OpenPin(PinMap.RefillPump, PinMode.Output);
            _feedPump = gpio.OpenPin(PinMap.FeedPump, PinMode.Output);
            _fan = gpio.OpenPin(PinMap.Fan, PinMode.Output);
            _nestFan = gpio.OpenPin(PinMap.NestFan, PinMode.Output);
            _statusLed = gpio.OpenPin(PinMap.StatusLed, PinMode.Output);

            // All three rings are chained on one data line and driven over the ESP32's RMT
            // peripheral, so the SPI bus stays free.
            _rings = new Ws2812b(PinMap.RiserLedData, riserCount * _ledsPerRing);

            AllOff();
        }

        public void SetHeater(bool on)
        {
            Write(_heater, on);
        }

        public void SetRefillPump(bool on)
        {
            Write(_refillPump, on);
        }

        public void SetFeedPump(bool on)
        {
            Write(_feedPump, on);
        }

        public void SetFan(bool on)
        {
            Write(_fan, on);
        }

        public void SetNestFan(bool on)
        {
            Write(_nestFan, on);
        }

        public void SetRiserLights(RgbColor[] colors)
        {
            for (int riser = 0; riser < _riserCount; riser++)
            {
                RgbColor color = riser < colors.Length ? colors[riser] : RgbColor.Black;

                for (int led = 0; led < _ledsPerRing; led++)
                {
                    _rings.Image.SetPixel((riser * _ledsPerRing) + led, 0, color.R, color.G, color.B);
                }
            }

            _rings.Update();
        }

        public void AllOff()
        {
            Write(_heater, false);
            Write(_refillPump, false);
            Write(_feedPump, false);
            Write(_fan, false);
            Write(_nestFan, false);

            RgbColor[] dark = new RgbColor[_riserCount];
            SetRiserLights(dark);
        }

        /// <summary>Heartbeat, so a controller that has locked up is visible from across the room.</summary>
        public void SetStatusLed(bool on)
        {
            Write(_statusLed, on);
        }

        private static void Write(GpioPin pin, bool on)
        {
            pin.Write(on ? PinValue.High : PinValue.Low);
        }
    }
}
