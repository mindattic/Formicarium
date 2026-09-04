using System;
using Formicarium.Core.Config;
using Formicarium.Core.Control;
using Formicarium.Core.Sensing;
using Xunit;

namespace Formicarium.Controller.Tests
{
    /// <summary>
    /// Hydration replaced a pump that sprayed the nest directly. These tests pin the properties
    /// that change bought — above all that hydration survives a dead soil probe, which the old
    /// design did not.
    /// </summary>
    public class HydrationTests
    {
        private const bool Full = true;
        private const bool Low = false;

        private static readonly DateTime T0 = TestDefaults.StartUtc;

        private static Reading Soil(double percent, DateTime at) => Reading.Good(percent, at);

        [Fact]
        public void RefillsWhenTheReservoirIsLow()
        {
            HydrationController hydration = new HydrationController(new Setpoints());

            Assert.True(hydration.Update(Low, Soil(50.0, T0), T0));
            Assert.True(hydration.IsRefilling);
            Assert.True(hydration.RefillWanted);
        }

        [Fact]
        public void DoesNothingWhenTheReservoirIsFull()
        {
            HydrationController hydration = new HydrationController(new Setpoints());

            Assert.False(hydration.Update(Full, Soil(50.0, T0), T0));
            Assert.False(hydration.RefillWanted);
        }

        [Fact]
        public void StopsAsSoonAsTheReservoirReadsFull()
        {
            HydrationController hydration = new HydrationController(new Setpoints());
            hydration.Update(Low, Soil(50.0, T0), T0);

            DateTime t = T0.AddSeconds(10);

            Assert.False(hydration.Update(Full, Soil(50.0, t), t));
            Assert.False(hydration.HitMaxRuntime);
        }

        [Fact]
        public void HydrationSurvivesADeadSoilProbe()
        {
            HydrationController hydration = new HydrationController(new Setpoints());

            // This is the whole point of moving the pump off the nest. Refilling keys off the
            // reservoir float switch, not the moisture probe, so a dead probe degrades the system
            // to "passively hydrated but unmonitored" rather than to "not hydrated at all".
            //
            // Under the previous design a failed probe stopped watering entirely, and the colony
            // would have dried out while every actuator sat correctly in its fail-safe state.
            Assert.True(hydration.Update(Low, Reading.Bad(T0), T0));
            Assert.True(hydration.IsRefilling);
        }

        [Fact]
        public void AStuckFloatSwitchCannotRunThePumpForever()
        {
            Setpoints setpoints = new Setpoints();
            HydrationController hydration = new HydrationController(setpoints);

            DateTime t = T0;
            bool running = hydration.Update(Low, Soil(50.0, t), t);
            Assert.True(running);

            for (int second = 1; second <= setpoints.RefillMaxRunSeconds; second++)
            {
                t = T0.AddSeconds(second);
                running = hydration.Update(Low, Soil(50.0, t), t);
            }

            Assert.False(running);
            Assert.True(hydration.HitMaxRuntime);
        }

        [Fact]
        public void RepeatedTimeoutsRaiseTheSupplyEmptyAlarm()
        {
            Setpoints setpoints = new Setpoints();
            HydrationController hydration = new HydrationController(setpoints);

            DateTime t = T0;

            for (int attempt = 0; attempt < setpoints.RefillCutoffsBeforeSupplyAlarm; attempt++)
            {
                hydration.Update(Low, Soil(50.0, t), t);

                for (int second = 1; second <= setpoints.RefillMaxRunSeconds; second++)
                {
                    t = t.AddSeconds(1);
                    hydration.Update(Low, Soil(50.0, t), t);
                }

                t = t.AddSeconds(setpoints.RefillCooldownSeconds + 1);
            }

            // Three timeouts in a row is not an air lock. The supply container is empty, which is
            // the one piece of routine maintenance this design still needs from a human.
            Assert.True(hydration.WaterSupplyEmpty);
        }

        [Fact]
        public void ASingleTimeoutDoesNotCryWolfAboutTheSupply()
        {
            Setpoints setpoints = new Setpoints();
            HydrationController hydration = new HydrationController(setpoints);

            DateTime t = T0;
            hydration.Update(Low, Soil(50.0, t), t);

            for (int second = 1; second <= setpoints.RefillMaxRunSeconds; second++)
            {
                t = T0.AddSeconds(second);
                hydration.Update(Low, Soil(50.0, t), t);
            }

            Assert.True(hydration.HitMaxRuntime);
            Assert.False(hydration.WaterSupplyEmpty);
        }

        [Fact]
        public void ASuccessfulRefillClearsTheSupplyAlarm()
        {
            Setpoints setpoints = new Setpoints();
            HydrationController hydration = new HydrationController(setpoints);

            DateTime t = T0;

            for (int attempt = 0; attempt < setpoints.RefillCutoffsBeforeSupplyAlarm; attempt++)
            {
                hydration.Update(Low, Soil(50.0, t), t);

                for (int second = 1; second <= setpoints.RefillMaxRunSeconds; second++)
                {
                    t = t.AddSeconds(1);
                    hydration.Update(Low, Soil(50.0, t), t);
                }

                t = t.AddSeconds(setpoints.RefillCooldownSeconds + 1);
            }

            Assert.True(hydration.WaterSupplyEmpty);

            // Someone refilled the jug. The alarm must clear on its own once water flows again,
            // rather than needing a button pressed, or it will be ignored next time.
            hydration.Update(Low, Soil(50.0, t), t);
            t = t.AddSeconds(5);
            hydration.Update(Full, Soil(50.0, t), t);

            Assert.False(hydration.WaterSupplyEmpty);
        }

        [Fact]
        public void CooldownSpacesOutRefillAttempts()
        {
            Setpoints setpoints = new Setpoints();
            HydrationController hydration = new HydrationController(setpoints);

            hydration.Update(Low, Soil(50.0, T0), T0);

            DateTime filled = T0.AddSeconds(10);
            hydration.Update(Full, Soil(50.0, filled), filled);

            DateTime tooSoon = filled.AddSeconds(30);
            Assert.False(hydration.Update(Low, Soil(50.0, tooSoon), tooSoon));

            DateTime allowed = filled.AddSeconds(setpoints.RefillCooldownSeconds + 1);
            Assert.True(hydration.Update(Low, Soil(50.0, allowed), allowed));
        }

        [Fact]
        public void AnOverWetNestHoldsOffRefillsAndRaisesAFault()
        {
            HydrationController hydration = new HydrationController(new Setpoints());

            // Wicking is running away, or the probe is wrong. Topping up the reservoir would still
            // be harmless — that is what sizing it buys — but there is no reason to, and holding
            // off makes the fault easier to read.
            Assert.False(hydration.Update(Low, Soil(95.0, T0), T0));
            Assert.True(hydration.SubstrateOverWet);
        }

        [Fact]
        public void ADryNestWithAFullReservoirIsReportedAsAWickingFailure()
        {
            HydrationController hydration = new HydrationController(new Setpoints());

            // Nothing the controller can do about this: the reservoir is full and the water is
            // not reaching the core. It is an alert, not a control input.
            hydration.Update(Full, Soil(5.0, T0), T0);

            Assert.True(hydration.SubstrateDry);
            Assert.False(hydration.IsRefilling);
        }

        [Fact]
        public void ImplausibleMoistureRaisesNeitherWetNorDryFault()
        {
            HydrationController hydration = new HydrationController(new Setpoints());

            // Outside 0-100 %, which is what a disconnected capacitive probe reads. It must not
            // be interpreted as either extreme.
            hydration.Update(Full, Reading.Good(-40.0, T0), T0);

            Assert.False(hydration.SubstrateOverWet);
            Assert.False(hydration.SubstrateDry);
        }

        [Fact]
        public void AcknowledgingClearsTheLatchedFaults()
        {
            Setpoints setpoints = new Setpoints();
            HydrationController hydration = new HydrationController(setpoints);

            DateTime t = T0;
            hydration.Update(Low, Soil(50.0, t), t);

            for (int second = 1; second <= setpoints.RefillMaxRunSeconds; second++)
            {
                t = T0.AddSeconds(second);
                hydration.Update(Low, Soil(50.0, t), t);
            }

            Assert.True(hydration.HitMaxRuntime);

            hydration.Acknowledge();

            Assert.False(hydration.HitMaxRuntime);
            Assert.False(hydration.WaterSupplyEmpty);
        }
    }
}
