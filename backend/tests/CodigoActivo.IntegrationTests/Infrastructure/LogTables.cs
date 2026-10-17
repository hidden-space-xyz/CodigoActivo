using CodigoActivo.Infrastructure.Logs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Reads and prepares the partitioned log tables of the test database.
/// </summary>
internal static class LogTables
{
    public static async Task TruncateAsync(PostgresContainerFixture postgres)
    {
        await ExecuteAsync(postgres, "TRUNCATE diagnostic_log_entries, audit_log_entries");
    }

    public static async Task<List<string>> PartitionsAsync(
        PostgresContainerFixture postgres,
        LogTable table
    )
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(TestCancellation.Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT child.relname
            FROM pg_inherits
            JOIN pg_class parent ON parent.oid = pg_inherits.inhparent
            JOIN pg_class child ON child.oid = pg_inherits.inhrelid
            WHERE parent.relname = @parent
            ORDER BY child.relname
            """,
            connection
        );
        command.Parameters.AddWithValue("parent", table.Name);

        var partitions = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(TestCancellation.Ct);
        while (await reader.ReadAsync(TestCancellation.Ct))
        {
            partitions.Add(reader.GetString(0));
        }

        return partitions;
    }

    public static Task CreatePartitionAsync(
        PostgresContainerFixture postgres,
        LogTable table,
        DateOnly day
    )
    {
        return ExecuteAsync(
            postgres,
            $"CREATE TABLE IF NOT EXISTS {LogPartitions.NameOf(table, day)} PARTITION OF {table.Name} "
                + $"FOR VALUES FROM ('{day:yyyy-MM-dd} 00:00:00+00') TO ('{day.AddDays(1):yyyy-MM-dd} 00:00:00+00')"
        );
    }

    public static Task DropPartitionAsync(
        PostgresContainerFixture postgres,
        LogTable table,
        DateOnly day
    )
    {
        return ExecuteAsync(postgres, $"DROP TABLE IF EXISTS {LogPartitions.NameOf(table, day)}");
    }

    public static async Task<List<DiagnosticLogEntry>> DiagnosticEntriesAsync(
        PostgresContainerFixture postgres
    )
    {
        await using var db = postgres.CreateContext();
        return await db.Set<DiagnosticLogEntry>()
            .AsNoTracking()
            .OrderBy(entry => entry.OccurredAt)
            .ToListAsync(TestCancellation.Ct);
    }

    public static async Task<List<AuditLogEntry>> AuditEntriesAsync(
        PostgresContainerFixture postgres
    )
    {
        await using var db = postgres.CreateContext();
        return await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(entry => entry.OccurredAt)
            .ToListAsync(TestCancellation.Ct);
    }

    private static async Task ExecuteAsync(PostgresContainerFixture postgres, string sql)
    {
        await using var db = postgres.CreateContext();
        await db.Database.ExecuteSqlRawAsync(sql, TestCancellation.Ct);
    }
}
