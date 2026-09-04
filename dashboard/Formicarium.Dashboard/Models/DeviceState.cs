namespace Formicarium.Dashboard.Models;

/// <summary>
/// Mirror of the firmware's published state. Deliberately a separate type from the firmware's
/// own <c>DeviceState</c> rather than a shared assembly: the firmware targets nanoFramework's
/// own mscorlib, so nothing can be shared with a normal .NET project except source, and this
/// side wants nullable annotations and records that the device side cannot use.
///
/// The <c>...Ok</c> flags are load-bearing. A false flag means the sensor failed and the value
/// is meaningless — never plot it, never average it, never fall back to it.
/// </summary>
public sealed class DeviceState
{
    public long TimestampUnixMs { get; set; }
    public long UptimeSeconds { get; set; }

    public double NestBottomTempC { get; set; }
    public bool NestBottomTempOk { get; set; }
    public double NestTopTempC { get; set; }
    public bool NestTopTempOk { get; set; }
    public double OutworldTempC { get; set; }
    public bool OutworldTempOk { get; set; }

    public double NestHumidityPct { get; set; }
    public bool NestHumidityOk { get; set; }
    public double OutworldHumidityPct { get; set; }
    public bool OutworldHumidityOk { get; set; }

    public double SoilMoisturePct { get; set; }
    public bool SoilMoistureOk { get; set; }
    public bool ReservoirFull { get; set; }

    public long[] RiserCounts { get; set; } = [];
    public double[] RiserRatesPerMinute { get; set; } = [];
    public long[] RiserDebouncedCounts { get; set; } = [];

    public int[] RiserChannel { get; set; } = [];
    public double[] RiserBrightness { get; set; } = [];
    public int AssignmentDayNumber { get; set; }
    public int LightingMode { get; set; }
    public bool LightCurfewActive { get; set; }

    public bool HeaterOn { get; set; }
    public bool RefillPumpOn { get; set; }
    public bool FeedPumpOn { get; set; }
    public bool FanOn { get; set; }
    public bool NestFanOn { get; set; }
    public bool RefillWanted { get; set; }

    public int Faults { get; set; }
    public bool ServiceMode { get; set; }

    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampUnixMs);
}

/// <summary>
/// Fault bits, kept in sync with <c>Formicarium.Core.Control.Faults</c> in the firmware.
/// </summary>
[Flags]
public enum Faults
{
    None = 0,
    NestBottomTempUnusable = 1,
    NestTopTempUnusable = 2,
    OutworldTempUnusable = 4,
    NestHumidityUnusable = 8,
    OutworldHumidityUnusable = 16,
    SoilMoistureUnusable = 32,
    NestOverTemp = 64,
    RefillCutoff = 128,
    NoValidReadingSinceBoot = 256,
    WaterSupplyEmpty = 512,
    SubstrateOverWet = 1024,
    SubstrateDry = 2048
}

public static class FaultText
{
    private static readonly (Faults Flag, string Text, bool Critical)[] Descriptions =
    [
        (Faults.NoValidReadingSinceBoot, "No sensor has read successfully since boot — everything is held off", true),
        (Faults.NestOverTemp, "A nest probe is above the cutout — heater forced off", true),
        (Faults.WaterSupplyEmpty, "Several refills timed out — the water supply container is almost certainly empty", true),
        (Faults.RefillCutoff, "A reservoir refill hit the hard cutoff — float switch stuck, or the pump is not moving water", true),
        (Faults.SubstrateDry, "Nest is dry despite a full reservoir — wicking has failed at the core contact face", true),
        (Faults.SubstrateOverWet, "Nest is wetter than it should be — wicking is running away, or the probe is wrong", false),
        (Faults.NestBottomTempUnusable, "Nest bottom probe unusable — this is the heater's control probe", true),
        (Faults.NestTopTempUnusable, "Nest top probe unusable — the gradient reading is lost", false),
        (Faults.OutworldTempUnusable, "Outworld probe unusable", false),
        (Faults.NestHumidityUnusable, "Nest humidity (SHT31-B) unusable — mould risk is unmonitored", false),
        (Faults.OutworldHumidityUnusable, "Outworld humidity (SHT31-A) unusable — fan is held off", false),
        (Faults.SoilMoistureUnusable, "Soil probe unusable — hydration continues from the reservoir, but unmonitored", false)
    ];

    public static IEnumerable<(string Text, bool Critical)> Describe(int mask)
    {
        var faults = (Faults)mask;

        foreach (var (flag, text, critical) in Descriptions)
        {
            if (faults.HasFlag(flag))
            {
                yield return (text, critical);
            }
        }
    }
}

public enum LightingMode
{
    /// <summary>Dark. The control condition — traffic logged with no light at all.</summary>
    Off = 0,

    /// <summary>Lit but carrying no traffic information. Separates colour response from response to change.</summary>
    Fixed = 1,

    /// <summary>The experiment: each ring's brightness follows its own riser's traffic.</summary>
    ClosedLoop = 2
}

public static class Channels
{
    public static string Name(int channel) => channel switch
    {
        0 => "Red",
        1 => "Green",
        2 => "Blue",
        _ => "?"
    };

    public static string Css(int channel) => channel switch
    {
        0 => "#d4614a",
        1 => "#7fb069",
        2 => "#5b8dd9",
        _ => "#6b625a"
    };
}
