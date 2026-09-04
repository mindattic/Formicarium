using Microsoft.Data.Sqlite;

namespace Formicarium.Dashboard.Services;

/// <summary>
/// Which build steps are done.
///
/// This lives in the same SQLite file as the telemetry rather than in the browser, because a
/// build checklist that is scoped to one browser profile is worse than useless: the guide is read
/// on a phone at the bench and on a laptop at the desk, and a step ticked in one place has to be
/// ticked in the other. There is exactly one column being built, so there is exactly one list.
/// </summary>
public sealed class BuildProgressStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public BuildProgressStore(IConfiguration configuration)
    {
        var path = configuration["Telemetry:DatabasePath"] ?? "formicarium.db";
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        Initialise();
    }

    private void Initialise()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        // Only completed steps are stored. An absent row means "not done", which keeps the table
        // correct when the sequence itself is edited — a step that no longer exists simply stops
        // being read, and a step that is inserted starts out unticked.
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS build_progress (
                step        INTEGER PRIMARY KEY,
                completed_at INTEGER NOT NULL
            );
            """;

        command.ExecuteNonQuery();
    }

    public async Task<HashSet<int>> CompletedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT step FROM build_progress;";

        var completed = new HashSet<int>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            completed.Add(reader.GetInt32(0));
        }

        return completed;
    }

    public async Task SetAsync(int step, bool done, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();

            if (done)
            {
                command.CommandText =
                    "INSERT OR REPLACE INTO build_progress (step, completed_at) VALUES ($step, $at);";
                command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            else
            {
                command.CommandText = "DELETE FROM build_progress WHERE step = $step;";
            }

            command.Parameters.AddWithValue("$step", step);

            await command.ExecuteNonQueryAsync(cancellationToken);
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
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM build_progress;";

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
