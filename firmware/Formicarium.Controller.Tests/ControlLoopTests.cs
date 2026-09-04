using System;
using Formicarium.Core.Config;
using Formicarium.Core.Control;
using Formicarium.Core.State;
using Xunit;

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

        [Fact]
        public void BootStateIsEverythingOff()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            Build(out sensors, out actuators, out setpoints);

            // Before a single tick. The MOSFET gate pulldowns hold the hardware off through
            // reset; this asserts the firmware agrees rather than leaving outputs undefined.
            Assert.Equal(1, actuators.AllOffCalls);
            Assert.False(actuators.HeaterOn);
            Assert.False(actuators.RefillPumpOn);
            Assert.False(actuators.FeedPumpOn);
            Assert.False(actuators.FanOn);
        }

        [Fact]
        public void NothingEnergisesUntilSomeSensorHasWorkedOnce()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.AllSensorsFailed = true;
            sensors.NestBottomTempC = 5.0; // freezing, and would normally demand heat

            DeviceState state = loop.Tick(T0.AddSeconds(1));

            Assert.False(state.HeaterOn);
            Assert.False(actuators.HeaterOn);

            // A controller that has never successfully read anything has no business switching
            // a heater on inside a sealed box.
            Assert.True(SafetyWatchdog.Has((Faults)state.Faults, Faults.NoValidReadingSinceBoot));
        }

        [Fact]
        public void HeatsAColdNest()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.NestBottomTempC = 22.0;

            DeviceState state = loop.Tick(T0.AddSeconds(1));

            Assert.True(state.HeaterOn);
            Assert.True(actuators.HeaterOn);
        }

        [Fact]
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

            Assert.False(state.HeaterOn);
            Assert.False(actuators.HeaterOn);
            Assert.True(SafetyWatchdog.Has((Faults)state.Faults, Faults.NestOverTemp));
        }

        [Fact]
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
            Assert.True(state.HeaterOn);
            Assert.True(state.RefillPumpOn);
            Assert.True(SafetyWatchdog.Has((Faults)state.Faults, Faults.SoilMoistureUnusable));
            Assert.False(SafetyWatchdog.Has((Faults)state.Faults, Faults.NestBottomTempUnusable));
        }

        [Fact]
        public void ServiceModeStopsEverythingWithoutClosingAnything()
        {
            FakeSensors sensors;
            FakeActuators actuators;
            Setpoints setpoints;
            ControlLoop loop = Build(out sensors, out actuators, out setpoints);

            sensors.NestBottomTempC = 20.0;
            loop.Tick(T0.AddSeconds(1));
            Assert.True(actuators.HeaterOn);

            loop.ServiceMode = true;
            DeviceState state = loop.Tick(T0.AddSeconds(2));

            Assert.True(state.ServiceMode);
            Assert.False(actuators.HeaterOn);
            Assert.False(actuators.RefillPumpOn);
            Assert.False(actuators.FanOn);

            // Nothing was closed. There is no gate to jam: the risers stay open and physical
            // containment during service is the manual plug caps.
            Assert.False(loop.RequestManualFeed(T0.AddSeconds(3)));
        }

        [Fact]
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

            Assert.True(state.HeaterOn);
        }

        [Fact]
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
            Assert.False(state.SoilMoistureOk);
            Assert.Equal(0.0, state.SoilMoisturePct, 6);
            Assert.True(state.NestBottomTempOk);
        }

        [Fact]
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

            Assert.True(
                actuators.HeaterTransitions <= 6,
                "heater switched " + actuators.HeaterTransitions + " times in 10 minutes at the setpoint");
        }

        [Fact]
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

            Assert.Equal(3, state.RiserRatesPerMinute.Length);
            Assert.Equal(1200, state.RiserCounts[0]);
            Assert.Equal(3, state.RiserChannel.Length);

            // Stimulus is published alongside response, because a traffic log without the light
            // level that produced it cannot be analysed afterwards.
            Assert.True(state.RiserBrightness[0] > state.RiserBrightness[1]);
            Assert.Equal(0.0, state.RiserBrightness[2], 6);
        }
    }
}
