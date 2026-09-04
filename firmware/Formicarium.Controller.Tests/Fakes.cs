using System;
using System.Collections.Generic;
using Formicarium.Core.Actuation;
using Formicarium.Core.Config;
using Formicarium.Core.Sensing;

namespace Formicarium.Controller.Tests
{
    /// <summary>
    /// Time under test control. Every timing rule in this firmware is measured in minutes to
    /// hours of real time, so being able to step a week forward instantly is the difference
    /// between testing the cooldown and hoping about it.
    /// </summary>
    public sealed class FakeClock : IClock
    {
        public FakeClock(DateTime startUtc)
        {
            UtcNow = startUtc;
        }

        public DateTime UtcNow { get; private set; }

        public void Advance(TimeSpan by)
        {
            UtcNow = UtcNow + by;
        }

        public void AdvanceSeconds(double seconds)
        {
            Advance(TimeSpan.FromSeconds(seconds));
        }
    }

    /// <summary>Sensors the test writes directly. Defaults to everything reading plausibly.</summary>
    public sealed class FakeSensors : ISensorSet
    {
        private readonly int _riserCount;

        public FakeSensors(int riserCount)
        {
            _riserCount = riserCount;
            NestBottomTempC = 27.0;
            NestTopTempC = 25.0;
            OutworldTempC = 23.0;
            NestHumidityPct = 70.0;
            OutworldHumidityPct = 55.0;
            SoilMoisturePct = 50.0;
            RiserRates = new double[riserCount];
            RiserCounts = new long[riserCount];
        }

        public double NestBottomTempC { get; set; }
        public double NestTopTempC { get; set; }
        public double OutworldTempC { get; set; }
        public double NestHumidityPct { get; set; }
        public double OutworldHumidityPct { get; set; }
        public double SoilMoisturePct { get; set; }

        /// <summary>When true, every sensor reports a failed read.</summary>
        public bool AllSensorsFailed { get; set; }

        /// <summary>When true, only the soil probe fails. Used to isolate hydration behaviour.</summary>
        public bool SoilSensorFailed { get; set; }

        /// <summary>Reservoir float switch. Starts full, so nothing refills unless a test asks.</summary>
        public bool ReservoirFull { get; set; } = true;

        public double[] RiserRates { get; private set; }
        public long[] RiserCounts { get; private set; }

        public int PollCount { get; private set; }

        public SensorSnapshot Poll(DateTime nowUtc)
        {
            PollCount++;

            SensorSnapshot snapshot = new SensorSnapshot(_riserCount);

            snapshot.ReservoirFull = ReservoirFull;

            if (AllSensorsFailed)
            {
                snapshot.NestBottomTempC = Reading.Bad(nowUtc);
                snapshot.NestTopTempC = Reading.Bad(nowUtc);
                snapshot.OutworldTempC = Reading.Bad(nowUtc);
                snapshot.NestHumidityPct = Reading.Bad(nowUtc);
                snapshot.OutworldHumidityPct = Reading.Bad(nowUtc);
                snapshot.SoilMoisturePct = Reading.Bad(nowUtc);
                return snapshot;
            }

            snapshot.NestBottomTempC = Reading.Good(NestBottomTempC, nowUtc);
            snapshot.NestTopTempC = Reading.Good(NestTopTempC, nowUtc);
            snapshot.OutworldTempC = Reading.Good(OutworldTempC, nowUtc);
            snapshot.NestHumidityPct = Reading.Good(NestHumidityPct, nowUtc);
            snapshot.OutworldHumidityPct = Reading.Good(OutworldHumidityPct, nowUtc);
            snapshot.SoilMoisturePct = SoilSensorFailed
                ? Reading.Bad(nowUtc)
                : Reading.Good(SoilMoisturePct, nowUtc);

            for (int i = 0; i < _riserCount; i++)
            {
                snapshot.RiserRatesPerMinute[i] = RiserRates[i];
                snapshot.RiserCounts[i] = RiserCounts[i];
            }

            return snapshot;
        }
    }

    /// <summary>Records what the control loop asked for, including the order of calls.</summary>
    public sealed class FakeActuators : IActuatorSet
    {
        public bool HeaterOn { get; private set; }
        public bool RefillPumpOn { get; private set; }
        public bool FeedPumpOn { get; private set; }
        public bool FanOn { get; private set; }
        public bool NestFanOn { get; private set; }
        public RgbColor[] Lights { get; private set; } = new RgbColor[0];

        public int AllOffCalls { get; private set; }

        /// <summary>Every state change, so tests can assert on transitions rather than only end state.</summary>
        public List<string> Journal { get; } = new List<string>();

        public void SetHeater(bool on)
        {
            if (on != HeaterOn)
            {
                Journal.Add("heater=" + on);
            }

            HeaterOn = on;
        }

        public void SetRefillPump(bool on)
        {
            if (on != RefillPumpOn)
            {
                Journal.Add("refill=" + on);
            }

            RefillPumpOn = on;
        }

        public void SetFeedPump(bool on)
        {
            if (on != FeedPumpOn)
            {
                Journal.Add("feed=" + on);
            }

            FeedPumpOn = on;
        }

        public void SetFan(bool on)
        {
            if (on != FanOn)
            {
                Journal.Add("fan=" + on);
            }

            FanOn = on;
        }

        public void SetNestFan(bool on)
        {
            if (on != NestFanOn)
            {
                Journal.Add("nestFan=" + on);
            }

            NestFanOn = on;
        }

        public void SetRiserLights(RgbColor[] colors)
        {
            Lights = colors;
        }

        public void AllOff()
        {
            AllOffCalls++;
            HeaterOn = false;
            RefillPumpOn = false;
            FeedPumpOn = false;
            FanOn = false;
            NestFanOn = false;
            Lights = new RgbColor[0];
        }

        public int HeaterTransitions
        {
            get
            {
                int n = 0;

                foreach (string entry in Journal)
                {
                    if (entry.StartsWith("heater="))
                    {
                        n++;
                    }
                }

                return n;
            }
        }
    }

    public static class TestDefaults
    {
        public const int RiserCount = 3;

        /// <summary>A Monday, mid-morning local, well away from midnight and the feed hour.</summary>
        public static DateTime StartUtc
        {
            get { return new DateTime(2026, 6, 1, 16, 0, 0); }
        }

        public static Setpoints Setpoints()
        {
            return new Setpoints();
        }
    }
}
