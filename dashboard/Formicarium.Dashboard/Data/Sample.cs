namespace Formicarium.Dashboard.Data;

/// <summary>
/// One row of the telemetry log: a full sensor/actuator snapshot at a point in time.
///
/// Failed sensors are stored as null rather than as a value plus a flag, so a bad reading is
/// excluded from AVG/MIN instead of quietly averaging in as a zero.
/// </summary>
public sealed class Sample
{
    public long Ts { get; set; }

    public double? NestBottomC { get; set; }
    public double? NestTopC { get; set; }
    public double? OutworldC { get; set; }
    public double? NestRh { get; set; }
    public double? OutworldRh { get; set; }
    public double? SoilPct { get; set; }

    public bool Heater { get; set; }
    public bool Refill { get; set; }
    public bool Feed { get; set; }
    public bool Fan { get; set; }
    public bool NestFan { get; set; }

    public int Faults { get; set; }
    public int LightingMode { get; set; }
}
