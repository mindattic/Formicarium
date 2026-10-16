using System;
using Formicarium.Core.Config;
using Formicarium.Core.Control;
using Formicarium.Core.Sensing;
using NUnit.Framework;

namespace Formicarium.Controller.Tests
{
    /// <summary>
    /// SafetyWatchdog is the cross-sensor heater kill switch: until now it was only exercised
    /// indirectly through ControlLoopTests. These pin its fault bits directly, including the
    /// DS18B20 power-on-default case that plausibility checking exists to catch.
    /// </summary>
    public class SafetyWatchdogTests
    {
        private static readonly DateTime T0 = TestDefaults.StartUtc;

        private static readonly Faults AllUnusableBits =
            Faults.NestBottomTempUnusable | Faults.NestTopTempUnusable | Faults.OutworldTempUnusable |
            Faults.NestHumidityUnusable | Faults.OutworldHumidityUnusable | Faults.SoilMoistureUnusable;

        private static SensorSnapshot HealthySnapshot(DateTime t)
        {
            SensorSnapshot snapshot = new SensorSnapshot(0);
            snapshot.NestBottomTempC = Reading.Good(27.0, t);
            snapshot.NestTopTempC = Reading.Good(25.0, t);
            snapshot.OutworldTempC = Reading.Good(23.0, t);
            snapshot.NestHumidityPct = Reading.Good(70.0, t);
            snapshot.OutworldHumidityPct = Reading.Good(55.0, t);
            snapshot.SoilMoisturePct = Reading.Good(50.0, t);
            return snapshot;
        }

        /// <summary>A reservoir that is already full and a mid-range, plausible soil reading: nothing for hydration to do.</summary>
        private static HydrationController HealthyHydration(Setpoints setpoints, DateTime t)
        {
            HydrationController hydration = new HydrationController(setpoints);
            hydration.Update(true, Reading.Good(50.0, t), t);
            return hydration;
        }

        private static void AssertOnlyThisSensorGoesUnusable(
            SafetyWatchdog watchdog,
            HydrationController hydration,
            DateTime baseline,
            DateTime nowUtc,
            Action<SensorSnapshot> breakSensor,
            Faults expected)
        {
            SensorSnapshot snapshot = HealthySnapshot(baseline);
            breakSensor(snapshot);

            Faults faults = watchdog.Evaluate(snapshot, nowUtc, hydration, hasSeenAnyValidReading: true);

            Assert.That(faults & AllUnusableBits, Is.EqualTo(expected));
        }

        [Test]
        public void EverythingHealthyRaisesNoFaults()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = HealthyHydration(setpoints, T0);

            Faults faults = watchdog.Evaluate(HealthySnapshot(T0), T0, hydration, hasSeenAnyValidReading: true);

            Assert.That(faults, Is.EqualTo(Faults.None));
        }

        [Test]
        public void ABadReadingSetsOnlyThatSensorsUnusableBit()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = HealthyHydration(setpoints, T0);

            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.NestBottomTempC = Reading.Bad(T0), Faults.NestBottomTempUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.NestTopTempC = Reading.Bad(T0), Faults.NestTopTempUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.OutworldTempC = Reading.Bad(T0), Faults.OutworldTempUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.NestHumidityPct = Reading.Bad(T0), Faults.NestHumidityUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.OutworldHumidityPct = Reading.Bad(T0), Faults.OutworldHumidityUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.SoilMoisturePct = Reading.Bad(T0), Faults.SoilMoistureUnusable);
        }

        [Test]
        public void AStaleReadingSetsOnlyThatSensorsUnusableBit()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = HealthyHydration(setpoints, T0);

            // Older than MaxReadingAgeSeconds as measured at T0, while every other reading in the
            // snapshot stays freshly timestamped at T0 itself.
            DateTime stale = T0.AddSeconds(-(setpoints.MaxReadingAgeSeconds + 1));

            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.NestBottomTempC = Reading.Good(27.0, stale), Faults.NestBottomTempUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.NestTopTempC = Reading.Good(25.0, stale), Faults.NestTopTempUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.OutworldTempC = Reading.Good(23.0, stale), Faults.OutworldTempUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.NestHumidityPct = Reading.Good(70.0, stale), Faults.NestHumidityUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.OutworldHumidityPct = Reading.Good(55.0, stale), Faults.OutworldHumidityUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.SoilMoisturePct = Reading.Good(50.0, stale), Faults.SoilMoistureUnusable);
        }

        [Test]
        public void AnOutOfPlausibleRangeReadingSetsOnlyThatSensorsUnusableBit()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = HealthyHydration(setpoints, T0);

            // 85.0 C is a DS18B20's power-on default: a perfectly well-formed, valid reading that
            // is nonetheless physically impossible on this hardware. Plausibility is what catches
            // it, since validity alone would not.
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.NestBottomTempC = Reading.Good(85.0, T0), Faults.NestBottomTempUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.NestTopTempC = Reading.Good(85.0, T0), Faults.NestTopTempUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.OutworldTempC = Reading.Good(85.0, T0), Faults.OutworldTempUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.NestHumidityPct = Reading.Good(150.0, T0), Faults.NestHumidityUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.OutworldHumidityPct = Reading.Good(-10.0, T0), Faults.OutworldHumidityUnusable);
            AssertOnlyThisSensorGoesUnusable(watchdog, hydration, T0, T0,
                s => s.SoilMoisturePct = Reading.Good(200.0, T0), Faults.SoilMoistureUnusable);
        }

        [Test]
        public void NestOverTempFiresWhenTheBottomProbeExceedsTheCutout()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = HealthyHydration(setpoints, T0);

            SensorSnapshot snapshot = HealthySnapshot(T0);
            snapshot.NestBottomTempC = Reading.Good(setpoints.NestOverTempCutoutC + 1.0, T0);

            Faults faults = watchdog.Evaluate(snapshot, T0, hydration, hasSeenAnyValidReading: true);

            Assert.That(SafetyWatchdog.Has(faults, Faults.NestOverTemp), Is.True);
        }

        [Test]
        public void NestOverTempFiresWhenTheTopProbeExceedsTheCutoutEvenIfTheControlProbeReadsCold()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = HealthyHydration(setpoints, T0);

            SensorSnapshot snapshot = HealthySnapshot(T0);
            snapshot.NestBottomTempC = Reading.Good(20.0, T0); // control probe: cold and plausible
            snapshot.NestTopTempC = Reading.Good(setpoints.NestOverTempCutoutC + 1.0, T0);

            Faults faults = watchdog.Evaluate(snapshot, T0, hydration, hasSeenAnyValidReading: true);

            Assert.That(SafetyWatchdog.Has(faults, Faults.NestOverTemp), Is.True);
        }

        [Test]
        public void NestOverTempDoesNotFireWhenTheOverLimitProbeIsItselfUnusable()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = HealthyHydration(setpoints, T0);

            SensorSnapshot snapshot = HealthySnapshot(T0);

            // 85.0 C clears the cutout but also clears TempPlausibleMaxC, so it reads as the
            // DS18B20 power-on default rather than a real over-temperature event. A probe that
            // cannot be trusted at all must not be allowed to trip the cutout on its own say-so.
            snapshot.NestBottomTempC = Reading.Good(85.0, T0);

            Faults faults = watchdog.Evaluate(snapshot, T0, hydration, hasSeenAnyValidReading: true);

            Assert.That(SafetyWatchdog.Has(faults, Faults.NestBottomTempUnusable), Is.True);
            Assert.That(SafetyWatchdog.Has(faults, Faults.NestOverTemp), Is.False);
        }

        [Test]
        public void RefillCutoffMirrorsHydrationHitMaxRuntime()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = new HydrationController(setpoints);

            DateTime t = T0;
            hydration.Update(false, Reading.Good(50.0, t), t);

            for (int second = 1; second <= setpoints.RefillMaxRunSeconds; second++)
            {
                t = T0.AddSeconds(second);
                hydration.Update(false, Reading.Good(50.0, t), t);
            }

            Assert.That(hydration.HitMaxRuntime, Is.True);

            Faults faults = watchdog.Evaluate(HealthySnapshot(t), t, hydration, hasSeenAnyValidReading: true);

            Assert.That(SafetyWatchdog.Has(faults, Faults.RefillCutoff), Is.True);
        }

        [Test]
        public void WaterSupplyEmptyMirrorsHydrationController()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = new HydrationController(setpoints);

            DateTime t = T0;

            for (int attempt = 0; attempt < setpoints.RefillCutoffsBeforeSupplyAlarm; attempt++)
            {
                hydration.Update(false, Reading.Good(50.0, t), t);

                for (int second = 1; second <= setpoints.RefillMaxRunSeconds; second++)
                {
                    t = t.AddSeconds(1);
                    hydration.Update(false, Reading.Good(50.0, t), t);
                }

                t = t.AddSeconds(setpoints.RefillCooldownSeconds + 1);
            }

            Assert.That(hydration.WaterSupplyEmpty, Is.True);

            Faults faults = watchdog.Evaluate(HealthySnapshot(t), t, hydration, hasSeenAnyValidReading: true);

            Assert.That(SafetyWatchdog.Has(faults, Faults.WaterSupplyEmpty), Is.True);
        }

        [Test]
        public void SubstrateOverWetMirrorsHydrationController()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = new HydrationController(setpoints);

            hydration.Update(false, Reading.Good(95.0, T0), T0);
            Assert.That(hydration.SubstrateOverWet, Is.True);

            Faults faults = watchdog.Evaluate(HealthySnapshot(T0), T0, hydration, hasSeenAnyValidReading: true);

            Assert.That(SafetyWatchdog.Has(faults, Faults.SubstrateOverWet), Is.True);
        }

        [Test]
        public void SubstrateDryMirrorsHydrationController()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = new HydrationController(setpoints);

            hydration.Update(true, Reading.Good(5.0, T0), T0);
            Assert.That(hydration.SubstrateDry, Is.True);

            Faults faults = watchdog.Evaluate(HealthySnapshot(T0), T0, hydration, hasSeenAnyValidReading: true);

            Assert.That(SafetyWatchdog.Has(faults, Faults.SubstrateDry), Is.True);
        }

        [Test]
        public void NoValidReadingSinceBootFiresOnlyWhenNothingHasEverBeenSeenValid()
        {
            Setpoints setpoints = new Setpoints();
            SafetyWatchdog watchdog = new SafetyWatchdog(setpoints);
            HydrationController hydration = HealthyHydration(setpoints, T0);

            Faults withoutBoot = watchdog.Evaluate(HealthySnapshot(T0), T0, hydration, hasSeenAnyValidReading: false);
            Faults withBoot = watchdog.Evaluate(HealthySnapshot(T0), T0, hydration, hasSeenAnyValidReading: true);

            Assert.That(SafetyWatchdog.Has(withoutBoot, Faults.NoValidReadingSinceBoot), Is.True);
            Assert.That(SafetyWatchdog.Has(withBoot, Faults.NoValidReadingSinceBoot), Is.False);
        }

        [Test]
        public void HasTestsASingleBitWithoutBeingTrippedByOthers()
        {
            Faults faults = Faults.NestOverTemp | Faults.WaterSupplyEmpty;

            Assert.That(SafetyWatchdog.Has(faults, Faults.NestOverTemp), Is.True);
            Assert.That(SafetyWatchdog.Has(faults, Faults.WaterSupplyEmpty), Is.True);
            Assert.That(SafetyWatchdog.Has(faults, Faults.SubstrateDry), Is.False);
        }

        [Test]
        public void AnyReadingValidIsFalseOnlyWhenEverySensorIsInvalid()
        {
            SensorSnapshot allBad = new SensorSnapshot(0);
            allBad.NestBottomTempC = Reading.Bad(T0);
            allBad.NestTopTempC = Reading.Bad(T0);
            allBad.OutworldTempC = Reading.Bad(T0);
            allBad.NestHumidityPct = Reading.Bad(T0);
            allBad.OutworldHumidityPct = Reading.Bad(T0);
            allBad.SoilMoisturePct = Reading.Bad(T0);

            Assert.That(SafetyWatchdog.AnyReadingValid(allBad), Is.False);

            // Only the soil probe came back; that alone is enough to leave boot-gating.
            allBad.SoilMoisturePct = Reading.Good(50.0, T0);

            Assert.That(SafetyWatchdog.AnyReadingValid(allBad), Is.True);
        }
    }
}
