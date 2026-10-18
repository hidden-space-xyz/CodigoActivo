using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Keeps the daily partitions of the log tables: it creates today's and the next days' ahead of
/// time, drops those whose whole day is past the retention and removes the expired entries of the
/// partition the cutoff falls in. Every statement runs in a short transaction of its own that
/// gives up after a short lock wait, so maintenance never holds the tables for long.
/// </summary>
/// <param name="database">Connections of the log tables.</param>
/// <param name="retention">How long the entries of each log table are kept.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class LogPartitionMaintainer(
    LogDatabase database,
    LogRetentionOptions retention,
    IClock clock
)
{
    private const string LockTimeout = "SET LOCAL lock_timeout = '5s'";
    private const string DuplicateTable = "42P07";
    private const string UniqueViolation = "23505";

    /// <summary>
    /// Creates the partitions kept ready and removes everything past the retention, in every log
    /// table.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task MaintainAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        await using var connection = await database.OpenConnectionAsync(ct);

        foreach (var table in LogTable.All)
        {
            foreach (var day in LogPartitions.UpcomingDays(now))
            {
                await CreateAsync(connection, table, day, ct);
            }

            var cutoff = LogPartitions.CutoffOf(retention.Of(table), now);
            foreach (var partition in await PartitionsAsync(connection, table, ct))
            {
                if (
                    LogPartitions.TryParseDay(table, partition, out var day)
                    && LogPartitions.IsExpired(day, cutoff)
                )
                {
                    await ExecuteAsync(connection, $"DROP TABLE IF EXISTS {partition}", null, ct);
                }
            }

            await ExecuteAsync(
                connection,
                $"DELETE FROM {table.Name} WHERE occurred_at < @cutoff",
                cutoff,
                ct
            );
        }
    }

    /// <summary>
    /// Runs <see cref="MaintainAsync"/> and records a failure instead of throwing it, because no
    /// caller depends on maintenance to go on.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task TryMaintainAsync(ILogger logger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            await MaintainAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogMaintenanceFailed(ex);
        }
    }

    /// <summary>
    /// Creates the missing partitions of a table for the days of a batch that may still have one.
    /// </summary>
    /// <param name="connection">Open connection of the log database the batch is written on.</param>
    /// <param name="table">Log table.</param>
    /// <param name="days">UTC days the batch holds.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when at least one day is inside the window of
    /// the table, so writing the batch again may succeed.
    /// </returns>
    public async Task<bool> EnsurePartitionsAsync(
        NpgsqlConnection connection,
        LogTable table,
        IEnumerable<DateOnly> days,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(days);

        var now = clock.UtcNow;
        var tableRetention = retention.Of(table);
        var inWindow = days.Distinct()
            .Where(day => LogPartitions.IsWithinWindow(tableRetention, day, now))
            .Order()
            .ToList();
        if (inWindow.Count is 0)
        {
            return false;
        }

        foreach (var day in inWindow)
        {
            await CreateAsync(connection, table, day, ct);
        }

        return true;
    }

    private static async Task CreateAsync(
        NpgsqlConnection connection,
        LogTable table,
        DateOnly day,
        CancellationToken ct
    )
    {
        var from = Bound(day);
        var to = Bound(day.AddDays(1));
        try
        {
            await ExecuteAsync(
                connection,
                $"CREATE TABLE IF NOT EXISTS {LogPartitions.NameOf(table, day)} PARTITION OF {table.Name} "
                    + $"FOR VALUES FROM ('{from}') TO ('{to}')",
                null,
                ct
            );
        }
        catch (PostgresException ex) when (ex.SqlState is DuplicateTable or UniqueViolation) { }
    }

    private static string Bound(DateOnly day)
    {
        return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 00:00:00+00";
    }

    private static async Task<List<string>> PartitionsAsync(
        NpgsqlConnection connection,
        LogTable table,
        CancellationToken ct
    )
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT child.relname
            FROM pg_inherits
            JOIN pg_class parent ON parent.oid = pg_inherits.inhparent
            JOIN pg_class child ON child.oid = pg_inherits.inhrelid
            WHERE parent.relname = @parent
            """,
            connection
        );
        command.Parameters.AddWithValue("parent", table.Name);

        var partitions = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            partitions.Add(reader.GetString(0));
        }

        return partitions;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The statements only name the log tables and partitions this class computes from constants and dates; PostgreSQL takes no parameters for identifiers or partition bounds."
    )]
    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        DateTimeOffset? cutoff,
        CancellationToken ct
    )
    {
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var timeout = new NpgsqlCommand(LockTimeout, connection, transaction))
        {
            await timeout.ExecuteNonQueryAsync(ct);
        }

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            if (cutoff is { } instant)
            {
                command.Parameters.AddWithValue("cutoff", instant.ToUniversalTime());
            }

            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
