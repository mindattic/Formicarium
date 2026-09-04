namespace Formicarium.Dashboard.Data;

/// <summary>Only completed steps are stored — an absent row means "not done".</summary>
public sealed class BuildProgressEntry
{
    public int Step { get; set; }
    public long CompletedAt { get; set; }
}
