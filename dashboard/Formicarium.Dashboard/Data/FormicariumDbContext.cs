using Microsoft.EntityFrameworkCore;

namespace Formicarium.Dashboard.Data;

public sealed class FormicariumDbContext(DbContextOptions<FormicariumDbContext> options) : DbContext(options)
{
    public DbSet<Sample> Samples => Set<Sample>();
    public DbSet<RiserSample> RiserSamples => Set<RiserSample>();
    public DbSet<BuildProgressEntry> BuildProgress => Set<BuildProgressEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every key here is an externally supplied value (a timestamp, a step number) rather
        // than something the database should invent, so identity generation is switched off —
        // otherwise EF Core defaults a single-column int/long key to IDENTITY.
        modelBuilder.Entity<Sample>(entity =>
        {
            entity.HasKey(s => s.Ts);
            entity.Property(s => s.Ts).ValueGeneratedNever();
        });

        modelBuilder.Entity<RiserSample>(entity =>
        {
            entity.HasKey(r => new { r.Ts, r.Riser });
            entity.HasIndex(r => r.Channel);
            entity.HasIndex(r => new { r.DayNumber, r.Riser });
        });

        modelBuilder.Entity<BuildProgressEntry>(entity =>
        {
            entity.HasKey(b => b.Step);
            entity.Property(b => b.Step).ValueGeneratedNever();
        });
    }
}
