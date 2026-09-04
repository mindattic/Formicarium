using Formicarium.Core.Actuation;
using Formicarium.Core.Config;
using Formicarium.Core.Sensing;

namespace Formicarium.Dashboard.Services;

/// <summary>
/// A physical and behavioural model of the column, good enough to develop and demonstrate
/// against before any hardware exists.
///
/// The important design choice here is that the simulator does <b>not</b> reimplement the
/// control policy. It provides simulated sensors and actuators to the real
/// <c>Formicarium.Core.Control.ControlLoop</c> — the same source that ships to the ESP32 — so
/// what you watch on the dashboard is the actual firmware reacting to a plausible colony,
/// rather than a second model of what the firmware is supposed to do. A stub that agreed with
/// the firmware only because both were written from the same intention would prove nothing.
/// </summary>
public sealed class ColonySimulator
{
    private const double AmbientC = 21.0;

    /// <summary>
    /// How strongly each channel deters an ant, at equal emitted power.
    ///
    /// Ants are most sensitive to UV and blue-green and are effectively blind to deep red, so
    /// these are not three flavours of the same stimulus — they are three very different ones.
    /// This asymmetry is the substance of the experiment and the reason the daily rotation
    /// matters: without rotating, colour and tube position are hopelessly confounded.
    /// </summary>
    private static readonly double[] ChannelAversion = [0.05, 0.5, 1.0];

    /// <summary>
    /// Baseline preference per riser from geometry alone — distance to the dish, position under
    /// the lid. Deliberately unequal, so the rotation has a real confound to control for and
    /// the analysis is not trivially clean.
    /// </summary>
    private static readonly double[] GeometricPreference = [1.0, 0.9, 0.82];

    private readonly Random _random = new(20260601);
    private readonly int _riserCount;

    private double _nestBottomC = 22.0;
    private double _nestTopC = 21.5;
    private double _outworldC = 21.2;
    private double _soilPct = 55.0;

    // Hydration reservoir, in millilitres. Capacity is deliberately small: the whole safety
    // argument for moving the pump off the nest is that this vessel's entire contents cannot
    // harm the colony even if all of it reached the substrate at once.
    private const double ReservoirCapacityMl = 120.0;
    private const double PumpMlPerSecond = 8.0;
    // Starts part-drawn rather than brim-full, which is both the realistic steady state and the
    // only way a short session exercises the refill path at all: wicking is slow by design, so a
    // full reservoir would sit untouched for simulated days and the pump would never run.
    private double _reservoirMl = ReservoirCapacityMl * 0.45;
    private double _nestRh = 72.0;
    private double _outworldRh = 52.0;

    private readonly long[] _counts;
    private readonly double[] _rates;
    private readonly double[] _crossingCredit;

    public ColonySimulator(int riserCount)
    {
        _riserCount = riserCount;
        _counts = new long[riserCount];
        _rates = new double[riserCount];
        _crossingCredit = new double[riserCount];
    }

    /// <summary>Set true to watch the fail-safes engage: every sensor starts reporting failure.</summary>
    public bool SimulateSensorFailure { get; set; }

    /// <summary>Set true to watch the dry-nest alert: the soil probe sticks reporting bone dry.</summary>
    public bool SimulateStuckDryProbe { get; set; }

    /// <summary>Set true to watch the supply alarm engage: the pump runs but no water arrives.</summary>
    public bool SimulateSupplyEmpty { get; set; }

    /// <summary>Set true to watch the refill limiter engage: the float switch never reads full.</summary>
    public bool SimulateStuckFloatSwitch { get; set; }

    public double ReservoirMl => _reservoirMl;

    /// <summary>Float switch reading. Full is 95% rather than 100% so it has real hysteresis.</summary>
    public bool ReservoirFull =>
        !SimulateStuckFloatSwitch && _reservoirMl >= ReservoirCapacityMl * 0.95;

    public void Step(double seconds, bool heaterOn, bool refillOn, bool fanOn, DateTime simNowUtc, RgbColor[] lights)
    {
        StepThermal(seconds, heaterOn, fanOn);
        StepHydration(seconds, refillOn, fanOn);
        StepTraffic(seconds, simNowUtc, lights);
    }

    private void StepThermal(double seconds, bool heaterOn, bool fanOn)
    {
        // First-order lag toward ambient, plus the heat cable when it is on. Time constants are
        // chosen so a 6 kg column of damp substrate behaves like one: minutes, not seconds.
        const double CouplingToAmbient = 1.0 / 2400.0;
        const double HeaterAuthority = 1.0 / 900.0;

        double heatInput = heaterOn ? (34.0 - _nestBottomC) * HeaterAuthority : 0.0;
        _nestBottomC += ((AmbientC - _nestBottomC) * CouplingToAmbient + heatInput) * seconds;

        // The top of the column lags the bottom and sits between it and ambient. The gap between
        // them is the vertical gradient the colony actually wants, which is why the firmware
        // controls from the bottom probe and only reports the top.
        double towardBottom = (_nestBottomC - 1.8 - _nestTopC) / 1800.0;
        _nestTopC += towardBottom * seconds;

        double outworldTarget = AmbientC + (fanOn ? 0.2 : 0.9);
        _outworldC += (outworldTarget - _outworldC) / 600.0 * seconds;
    }

    private void StepHydration(double seconds, bool refillOn, bool fanOn)
    {
        // Two loops, deliberately at very different speeds.
        //
        // The pump fills the reservoir in seconds. The reservoir wicks into the Ytong core over
        // hours. That separation is the point of the design: the fast, electrically-driven part
        // cannot reach the nest, and the part that reaches the nest has no actuator at all.
        const double DryingPerSecond = 2.0 / 3600.0;

        if (refillOn && !SimulateSupplyEmpty)
        {
            _reservoirMl = Math.Min(ReservoirCapacityMl, _reservoirMl + PumpMlPerSecond * seconds);
        }

        _soilPct -= DryingPerSecond * seconds;

        // Wicking is demand-led: dry core pulls harder, and it stops entirely when the reservoir
        // is dry. Nothing here can be commanded, which is exactly why it cannot flood.
        if (_reservoirMl > 0.0)
        {
            double deficit = Math.Max(0.0, 60.0 - _soilPct);
            double wickMl = Math.Min(_reservoirMl, deficit * 0.0012 * seconds);

            _reservoirMl -= wickMl;
            _soilPct += wickMl * 0.35;
        }

        _soilPct = Math.Clamp(_soilPct, 0.0, 100.0);

        double nestRhTarget = 45.0 + _soilPct * 0.45;
        _nestRh += (nestRhTarget - _nestRh) / 300.0 * seconds;

        // The outworld sits above the nest and shares air with it through three open risers, so
        // it tracks nest humidity at a distance. The fan is what pulls it back down.
        double outworldTarget = fanOn ? 45.0 : 40.0 + _nestRh * 0.55;
        _outworldRh += (outworldTarget - _outworldRh) / 240.0 * seconds;

        _nestRh = Math.Clamp(_nestRh, 0.0, 100.0);
        _outworldRh = Math.Clamp(_outworldRh, 0.0, 100.0);
    }

    private void StepTraffic(double seconds, DateTime simNowUtc, RgbColor[] lights)
    {
        // Diurnal foraging: a broad daytime peak, near-silence overnight.
        double hour = simNowUtc.Hour + simNowUtc.Minute / 60.0;
        double daylight = Math.Max(0.0, Math.Sin((hour - 6.0) / 24.0 * 2.0 * Math.PI));
        double colonyActivity = 4.0 + 26.0 * daylight;

        double[] weights = new double[_riserCount];
        double total = 0.0;

        for (int riser = 0; riser < _riserCount; riser++)
        {
            double brightness = 0.0;
            double aversion = 0.0;

            if (riser < lights.Length)
            {
                RgbColor color = lights[riser];

                // Whichever channel is lit identifies the colour, and its level sets the dose.
                if (color.R >= color.G && color.R >= color.B)
                {
                    brightness = color.R / 255.0;
                    aversion = ChannelAversion[0];
                }
                else if (color.G >= color.B)
                {
                    brightness = color.G / 255.0;
                    aversion = ChannelAversion[1];
                }
                else
                {
                    brightness = color.B / 255.0;
                    aversion = ChannelAversion[2];
                }
            }

            // Exponential deterrence: a lit riser is used less, in proportion to how visible its
            // colour is to an ant. Red barely registers, blue is strongly avoided.
            weights[riser] = GeometricPreference[riser % GeometricPreference.Length]
                             * Math.Exp(-6.0 * aversion * brightness);

            total += weights[riser];
        }

        for (int riser = 0; riser < _riserCount; riser++)
        {
            double share = total > 0 ? weights[riser] / total : 1.0 / _riserCount;
            double crossingsPerSecond = colonyActivity * share / 60.0;

            // Poisson-ish jitter, so the rate traces look like a colony rather than a function.
            double expected = crossingsPerSecond * seconds;
            _crossingCredit[riser] += expected * (0.7 + 0.6 * _random.NextDouble());

            long whole = (long)_crossingCredit[riser];

            if (whole > 0)
            {
                _counts[riser] += whole;
                _crossingCredit[riser] -= whole;
            }

            // Same rolling-window semantics the firmware reports, smoothed for display.
            double instantaneous = crossingsPerSecond * 60.0;
            _rates[riser] += (instantaneous - _rates[riser]) * Math.Min(1.0, seconds / 120.0);
        }
    }

    public SensorSnapshot BuildSnapshot(DateTime nowUtc)
    {
        var snapshot = new SensorSnapshot(_riserCount);

        if (SimulateSensorFailure)
        {
            snapshot.NestBottomTempC = Reading.Bad(nowUtc);
            snapshot.NestTopTempC = Reading.Bad(nowUtc);
            snapshot.OutworldTempC = Reading.Bad(nowUtc);
            snapshot.NestHumidityPct = Reading.Bad(nowUtc);
            snapshot.OutworldHumidityPct = Reading.Bad(nowUtc);
            snapshot.SoilMoisturePct = Reading.Bad(nowUtc);
        }
        else
        {
            snapshot.NestBottomTempC = Reading.Good(Round(_nestBottomC), nowUtc);
            snapshot.NestTopTempC = Reading.Good(Round(_nestTopC), nowUtc);
            snapshot.OutworldTempC = Reading.Good(Round(_outworldC), nowUtc);
            snapshot.NestHumidityPct = Reading.Good(Round(_nestRh), nowUtc);
            snapshot.OutworldHumidityPct = Reading.Good(Round(_outworldRh), nowUtc);

            snapshot.SoilMoisturePct = Reading.Good(
                SimulateStuckDryProbe ? 4.0 : Round(_soilPct), nowUtc);
        }

        // Note this sits OUTSIDE the sensor-failure branch. The float switch is a mechanical
        // contact on its own pin, so it keeps working when the I2C and 1-Wire buses are dead —
        // which is the property that keeps the colony hydrated through a sensor failure.
        snapshot.ReservoirFull = ReservoirFull;

        for (int riser = 0; riser < _riserCount; riser++)
        {
            snapshot.RiserCounts[riser] = _counts[riser];
            snapshot.RiserRatesPerMinute[riser] = _rates[riser];
            snapshot.RiserDebouncedCounts[riser] = _counts[riser] / 7;
        }

        return snapshot;
    }

    /// <summary>Quantisation to roughly what the real sensors resolve, so the traces are not implausibly smooth.</summary>
    private static double Round(double value) => Math.Round(value, 2);
}
