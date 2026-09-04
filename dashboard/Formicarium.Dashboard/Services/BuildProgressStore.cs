using Formicarium.Dashboard.Data;
using Microsoft.EntityFrameworkCore;

namespace Formicarium.Dashboard.Services;

/// <summary>
/// Which build steps are done.
///
/// This lives in the same database as the telemetry rather than in the browser, because a
/// build checklist that is scoped to one browser profile is worse than useless: the guide is read
/// on a phone at the bench and on a laptop at the desk, and a step ticked in one place has to be
/// ticked in the other. There is exactly one column being built, so there is exactly one list.
/// </summary>
public sealed class BuildProgressStore(IDbContextFactory<FormicariumDbContext> contextFactory)
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<HashSet<int>> CompletedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var steps = await context.BuildProgress.Select(b => b.Step).ToListAsync(cancellationToken);
        return [.. steps];
    }

    public async Task SetAsync(int step, bool done, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var existing = await context.BuildProgress.FindAsync([step], cancellationToken);

            if (done)
            {
                if (existing is null)
                {
                    context.BuildProgress.Add(new BuildProgressEntry { Step = step, CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                }
            }
            else if (existing is not null)
            {
                context.BuildProgress.Remove(existing);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await context.BuildProgress.ExecuteDeleteAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
