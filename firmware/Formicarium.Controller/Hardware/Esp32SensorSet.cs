using System;
using System.Device.Adc;
using System.Device.Gpio;
using System.Device.I2c;
using Formicarium.Core.Config;
using Formicarium.Core.Sensing;
using Iot.Device.Sht3x;
using nanoFramework.Device.OneWire;

namespace Formicarium.Controller.Hardware
{
    /// <summary>
    /// Reads every sensor on the column.
    ///
    /// The governing rule here is that <b>a failure produces <see cref="Reading.Bad"/>, never a
    /// substitute value</b>. Every read is individually wrapped, so one dead probe degrades one
    /// measurement rather than throwing out of the control loop and taking the whole column with
    /// it. Everything upstream is built on the assumption that this class never lies, and a
    /// silent fallback to a plausible default would break that quietly.
    ///
    /// NOTE FOR BRING-UP: the exact call signatures of the Ds18b20, Sht3x and Ws28xx bindings
    /// track their NuGet packages and have not been executed against real hardware here. If a
    /// signature has moved, it will move inside these adapter methods only — Core is insulated
    /// from it by construction.
    /// </summary>
    public sealed class Esp32SensorSet : ISensorSet
    {
        private readonly OneWireHost _oneWire;
        private readonly Sht3x _outworldRh;
        private readonly Sht3x _nestRh;
        private readonly AdcChannel _soil;
        private readonly RiserTrafficCounter[] _traffic;
        private readonly GpioPin[] _beams;
        private readonly GpioPin _emitterEnable;
        private readonly GpioPin _reservoirLevel;

        private readonly byte[] _nestBottomRom;
        private readonly byte[] _nestTopRom;
        private readonly byte[] _outworldRom;

        // Raw ADC counts at the calibration extremes, captured during bring-up with the probe in
        // air and then in saturated substrate. Capacitive probes read HIGHER when DRIER, which is
        // the opposite of the intuitive direction, so the inversion happens here and Core only
        // ever sees a percentage where higher means wetter.
        private readonly int _soilCountsDry;
        private readonly int _soilCountsWet;

        public Esp32SensorSet(
            GpioController gpio,
            OneWireHost oneWire,
            I2cDevice outworldSht,
            I2cDevice nestSht,
            AdcChannel soil,
            byte[] nestBottomRom,
            byte[] nestTopRom,
            byte[] outworldRom,
            int soilCountsDry,
            int soilCountsWet,
            Setpoints setpoints)
        {
            _oneWire = oneWire;
            _outworldRh = new Sht3x(outworldSht);
            _nestRh = new Sht3x(nestSht);
            _soil = soil;
            _nestBottomRom = nestBottomRom;
            _nestTopRom = nestTopRom;
            _outworldRom = outworldRom;
            _soilCountsDry = soilCountsDry;
            _soilCountsWet = soilCountsWet;

            _traffic = new RiserTrafficCounter[PinMap.RiserCount];
            _beams = new GpioPin[PinMap.RiserCount];

            _emitterEnable = gpio.OpenPin(PinMap.BeamEmitterEnable, PinMode.Output);
            _emitterEnable.Write(PinValue.High);

            _reservoirLevel = gpio.OpenPin(PinMap.ReservoirLevel, PinMode.InputPullUp);

            for (int riser = 0; riser < PinMap.RiserCount; riser++)
            {
                _traffic[riser] = new RiserTrafficCounter(
                    setpoints.TrafficWindowSeconds, setpoints.BeamDebounceMs);

                GpioPin pin = gpio.OpenPin(PinMap.BeamReceivers[riser], PinMode.InputPullUp);
                pin.ValueChanged += OnBeamChanged;
                _beams[riser] = pin;
            }
        }

        public SensorSnapshot Poll(DateTime nowUtc)
        {
            SensorSnapshot snapshot = new SensorSnapshot(PinMap.RiserCount);

            snapshot.NestBottomTempC = ReadProbe(_nestBottomRom, nowUtc);
            snapshot.NestTopTempC = ReadProbe(_nestTopRom, nowUtc);
            snapshot.OutworldTempC = ReadProbe(_outworldRom, nowUtc);

            snapshot.NestHumidityPct = ReadHumidity(_nestRh, nowUtc);
            snapshot.OutworldHumidityPct = ReadHumidity(_outworldRh, nowUtc);

            snapshot.SoilMoisturePct = ReadSoil(nowUtc);

            // Float switch closes to ground when the float rises, so LOW means full. A broken
            // wire therefore reads "not full", which asks for a bounded refill rather than
            // silently stopping hydration - see PinMap.ReservoirLevel.
            snapshot.ReservoirFull = _reservoirLevel.Read() == PinValue.Low;

            for (int riser = 0; riser < PinMap.RiserCount; riser++)
            {
                // Ageing the window every pass matters even when nothing is crossing: without
                // it the rate would stay frozen at whatever the colony was doing when it went
                // quiet, and the lighting loop would follow a number that had stopped meaning
                // anything.
                _traffic[riser].Tick(nowUtc);

                snapshot.RiserCounts[riser] = _traffic[riser].CumulativeCount;
                snapshot.RiserRatesPerMinute[riser] = _traffic[riser].RatePerMinute;
                snapshot.RiserDebouncedCounts[riser] = _traffic[riser].DebouncedCount;
            }

            return snapshot;
        }

        private void OnBeamChanged(object sender, PinValueChangedEventArgs e)
        {
            // The receiver is open-collector and idles high through the internal pull-up, so an
            // occluded beam pulls it low. Only the falling edge is a crossing.
            if (e.ChangeType != PinEventTypes.Falling)
            {
                return;
            }

            for (int riser = 0; riser < PinMap.RiserCount; riser++)
            {
                if (e.PinNumber == PinMap.BeamReceivers[riser])
                {
                    _traffic[riser].RecordBreak(DateTime.UtcNow);
                    return;
                }
            }
        }

        private Reading ReadProbe(byte[] romId, DateTime nowUtc)
        {
            if (romId == null)
            {
                return Reading.Bad(nowUtc);
            }

            try
            {
                Iot.Device.Ds18b20.Ds18b20 probe = new Iot.Device.Ds18b20.Ds18b20(_oneWire, romId, false);

                if (!probe.TryReadTemperature(out nanoFramework.UnitsNet.Temperature temperature))
                {
                    return Reading.Bad(nowUtc);
                }

                return Reading.Good(temperature.DegreesCelsius, nowUtc);
            }
            catch
            {
                // A probe that has come loose throws rather than returning. Swallowing it here
                // is deliberate: the caller's contract is a Reading, and an exception escaping
                // into the control loop would take down four healthy subsystems with it.
                return Reading.Bad(nowUtc);
            }
        }

        private static Reading ReadHumidity(Sht3x sensor, DateTime nowUtc)
        {
            try
            {
                double rh = sensor.Humidity.Percent;

                return Reading.Good(rh, nowUtc);
            }
            catch
            {
                return Reading.Bad(nowUtc);
            }
        }

        private Reading ReadSoil(DateTime nowUtc)
        {
            try
            {
                int counts = _soil.ReadValue();

                int span = _soilCountsDry - _soilCountsWet;

                if (span == 0)
                {
                    // Un-calibrated. Reporting an arbitrary number here would be worse than
                    // reporting nothing, because the mister would act on it.
                    return Reading.Bad(nowUtc);
                }

                double percent = (_soilCountsDry - counts) * 100.0 / span;

                return Reading.Good(percent, nowUtc);
            }
            catch
            {
                return Reading.Bad(nowUtc);
            }
        }
    }
}
