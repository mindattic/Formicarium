using System;
using Formicarium.Core.Config;
using Formicarium.Core.Control;
using Formicarium.Core.State;
using NUnit.Framework;

namespace Formicarium.Controller.Tests
{
    /// <summary>
    /// Interaction tests. The individual controllers are pinned elsewhere; what matters here is
    /// that they compose without one subsystem's failure taking down an unrelated one, and that
    /// the cross-cutting rules — boot state, the over-temperature cutout, service mode — really
    /// do outrank normal control.
    /// </summary>
    public class ControlLoopTests
    {
        private static readonly DateTime T0 = TestDefaults.StartUtc;

        private static ControlLoop Build(out FakeSensors sensors, out FakeActuators actuators, out Setpoints setpoints)
        {
            sensors = new FakeSensors(TestDefaults.RiserCount);
            actuators = new FakeActuators();
            setpoints = new Setpoints();
            return new ControlLoop(sensors, actuators, setpoints, T0, TestDefaults.RiserCount);
        }

        [Test]
        public void BootStateIsEverythingOff()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            Build(out sensors, out actuators, out setpoints);

            // Before a single tick. The MOSFET gate pulldowns hold the hardware off through
            // reset; this asserts the firmware agrees rather than leaving outputs undefined.
            Assert.That(actuators.AllOffCalls, Is.EqualTo(1));
            Assert.That(actuators.HeaterOn, Is.False);
            Assert.That(actuators.RefillPumpOn, Is.False);
            Assert.That(actuators.FeedPumpOn, Is.False);
            Assert.That(actuators.FanOn, Is.False);
        }

        [Test]
        public void NothingEnergisesUntilSomeSensorHasWorkedOnce()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.AllSensorsFailed = true;
            sensors.NestBottomTempC = 5.0; // freezing, and would normally demand heat

            DeviceState state = loop.Tick(T0.AddSeconds(1));

            Assert.That(state.HeaterOn, Is.False);
            Assert.That(actuators.HeaterOn, Is.False);

            // A controller that has never successfully read anything has no business switching
            // a heater on inside a sealed box.
            Assert.That(SafetyWatchdog.Has((Faults)state.Faults, Faults.NoValidReadingSinceBoot), Is.True);
        }

        [Test]
        public void HeatsAColdNest()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.NestBottomTempC = 22.0;

            DeviceState state = loop.Tick(T0.AddSeconds(1));

            Assert.That(state.HeaterOn, Is.True);
            Assert.That(actuators.HeaterOn, Is.True);
        }

        [Test]
        public void ASecondProbeSeeingOverTemperatureKillsTheHeater()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            // The control probe reads cold and plausible, so ordinary hysteresis would call for
            // heat indefinitely. The nest-top probe says the colony is being cooked. The cutout
            // reads every nest probe precisely so that one failed-low sensor cannot do this.
            sensors.NestBottomTempC = 20.0;
            sensors.NestTopTempC = 40.0;

            DeviceState state = loop.Tick(T0.AddSeconds(1));

            Assert.That(state.HeaterOn, Is.False);
            Assert.That(actuators.HeaterOn, Is.False);
            Assert.That(SafetyWatchdog.Has((Faults)state.Faults, Faults.NestOverTemp), Is.True);
        }

        [Test]
        public void OneFailedSensorDoesNotShutDownUnrelatedSubsystems()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.NestBottomTempC = 22.0; // wants heat
            sensors.SoilSensorFailed = true;
            sensors.ReservoirFull = false;  // reservoir needs topping up

            DeviceState state = loop.Tick(T0.AddSeconds(1));

            // Fail-safe should be proportionate, and after moving the pump off the nest it can
            // afford to be. The soil probe is now a monitoring input, not a control input, so
            // losing it costs the fault flag and nothing else: the colony is still warm, and
            // still being hydrated from the reservoir.
            Assert.That(state.HeaterOn, Is.True);
            Assert.That(state.RefillPumpOn, Is.True);
            Assert.That(SafetyWatchdog.Has((Faults)state.Faults, Faults.SoilMoistureUnusable), Is.True);
            Assert.That(SafetyWatchdog.Has((Faults)state.Faults, Faults.NestBottomTempUnusable), Is.False);
        }

        [Test]
        public void ServiceModeStopsEverythingWithoutClosingAnything()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.NestBottomTempC = 20.0;
            loop.Tick(T0.AddSeconds(1));
            Assert.That(actuators.HeaterOn, Is.True);

            loop.ServiceMode = true;
            DeviceState state = loop.Tick(T0.AddSeconds(2));

            Assert.That(state.ServiceMode, Is.True);
            Assert.That(actuators.HeaterOn, Is.False);
            Assert.That(actuators.RefillPumpOn, Is.False);
            Assert.That(actuators.FanOn, Is.False);

            // Nothing was closed. There is no gate to jam: the risers stay open and physical
            // containment during service is the manual plug caps.
            Assert.That(loop.RequestManualFeed(T0.AddSeconds(3)), Is.False);
        }

        [Test]
        public void RecoversWhenServiceModeIsReleased()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.NestBottomTempC = 20.0;
            loop.Tick(T0.AddSeconds(1));

            loop.ServiceMode = true;
            loop.Tick(T0.AddSeconds(2));

            loop.ServiceMode = false;

            // Past the minimum-off dwell that the forced stop started.
            DeviceState state = loop.Tick(T0.AddSeconds(200));

            Assert.That(state.HeaterOn, Is.True);
        }

        [Test]
        public void StatePublishesInvalidReadingsAsNotOkRatherThanAsPlausibleNumbers()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            loop.Tick(T0.AddSeconds(1));
            sensors.SoilSensorFailed = true;

            DeviceState state = loop.Tick(T0.AddSeconds(2));

            // A consumer that ignores the Ok flag gets a zero, which is visibly wrong, rather
            // than a stale number, which is not.
            Assert.That(state.SoilMoistureOk, Is.False);
            Assert.That(state.SoilMoisturePct, Is.EqualTo(0.0).Within(5e-7));
            Assert.That(state.NestBottomTempOk, Is.True);
        }

        [Test]
        public void HeaterDoesNotChatterAcrossManyTicksAtTheSetpoint()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            // Sit exactly on the lower threshold with noise either side of it, ticking once a
            // second for ten minutes. A relay clicking on and off every second would wear out
            // the MOSFET's load and make the temperature record useless.
            double[] noise = new double[] { -0.05, 0.05, -0.02, 0.03, 0.0 };

            for (int second = 1; second <= 600; second++)
            {
                sensors.NestBottomTempC = setpoints.NestHeatOnBelowC + noise[second % noise.Length];
                loop.Tick(T0.AddSeconds(second));
            }

            Assert.That(actuators.HeaterTransitions <= 6, Is.True, "heater switched " + actuators.HeaterTransitions + " times in 10 minutes at the setpoint");
        }

        [Test]
        public void NestExhaustRunsOnNestHumidityNotOutworldHumidity()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            // The two fans watch different zones, and it matters which: the nest is deliberately
            // kept humid, so a nest exhaust driven off outworld air would run more or less
            // permanently and fight the wicking core.
            sensors.NestHumidityPct = 90.0;      // mould territory
            sensors.OutworldHumidityPct = 40.0;  // outworld is fine

            DeviceState state = loop.Tick(T0.AddSeconds(1));

            Assert.That(state.NestFanOn, Is.True);
            Assert.That(state.FanOn, Is.False);
        }

        [Test]
        public void NestExhaustLeavesAHumidNestAloneBelowTheMouldThreshold()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            // 70% is where the colony wants to live. Venting here would just pump the reservoir
            // dry through the ceiling.
            sensors.NestHumidityPct = 70.0;

            DeviceState state = loop.Tick(T0.AddSeconds(1));

            Assert.That(state.NestFanOn, Is.False);
        }

        [Test]
        public void NestExhaustStopsWhenItsOwnSensorFails()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.NestHumidityPct = 95.0;
            loop.Tick(T0.AddSeconds(1));
            Assert.That(actuators.NestFanOn, Is.True);

            // Losing SHT31-B means mould risk becomes unmonitored, which is bad - but venting
            // blind would dry the nest indefinitely, which is worse. Off is the right failure.
            sensors.AllSensorsFailed = true;
            DeviceState state = loop.Tick(T0.AddSeconds(400));

            Assert.That(state.NestFanOn, Is.False);
            Assert.That(SafetyWatchdog.Has((Faults)state.Faults, Faults.NestHumidityUnusable), Is.True);
        }

        [Test]
        public void TrafficAndLightingAppearInPublishedState()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.RiserRates[0] = 30.0;
            sensors.RiserRates[1] = 5.0;
            sensors.RiserRates[2] = 0.0;
            sensors.RiserCounts[0] = 1200;

            DeviceState state = loop.Tick(T0.AddSeconds(1));

            Assert.That(state.RiserRatesPerMinute.Length, Is.EqualTo(3));
            Assert.That(state.RiserCounts[0], Is.EqualTo(1200));
            Assert.That(state.RiserChannel.Length, Is.EqualTo(3));

            // Stimulus is published alongside response, because a traffic log without the light
            // level that produced it cannot be analysed afterwards.
            Assert.That(state.RiserBrightness[0] > state.RiserBrightness[1], Is.True);
            Assert.That(state.RiserBrightness[2], Is.EqualTo(0.0).Within(5e-7));
        }
    }
}
