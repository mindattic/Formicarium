namespace Formicarium.Dashboard.Data;

/// <summary>
/// One riser's traffic count at a point in time, carrying the experimental covariates
/// (channel colour, brightness) that produced it. Composite key on (Ts, Riser).
/// </summary>
public sealed class RiserSample
{
    public long Ts { get; set; }
    public int Riser { get; set; }

    public long Cumulative { get; set; }
    public long Crossings { get; set; }
    public double Rate { get; set; }
    public int Channel { get; set; }
    public double Brightness { get; set; }
    public int DayNumber { get; set; }
}
