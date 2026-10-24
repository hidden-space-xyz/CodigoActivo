using Npgsql;
using NpgsqlTypes;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Writes batches of log events into a partitioned log table with one binary <c>COPY</c>. The
/// batching sink of Serilog calls it from its own background loop, so nothing here ever runs on a
/// request. Before the first batch of each day it creates the partitions the batch needs that are
/// still inside the retention window, so a history of past days, such as the one of demo mode, or
/// a start after the partitions ahead ran out is written without a failed copy first. A batch the
/// database refuses for its data is written again row by row and only the refused rows are
/// dropped; a batch that still finds no partition for its day, because one was dropped since,
/// creates the partitions of the batch and is written once more. Any other failure, such as an
/// unreachable database, is thrown so Serilog retries the whole batch with backoff until its retry
/// limit.
/// </summary>
/// <typeparam name="TRow">Row type the events become.</typeparam>
/// <param name="database">Connections of the log tables, owned by the host.</param>
/// <param name="partitions">Maintenance of the log partitions.</param>
/// <param name="table">Table the rows go to.</param>
public abstract class PartitionedLogSink<TRow>(
    LogDatabase database,
    LogPartitionMaintainer partitions,
    LogTable table
) : IBatchedLogEventSink
{
    private const string NoPartitionForRow = "23514";
    private const string DataExceptionClass = "22";
    private const string IntegrityViolationClass = "23";

    private readonly HashSet<DateOnly> preparedDays = [];

    /// <summary>
    /// Gets the <c>COPY … FROM STDIN (FORMAT BINARY)</c> statement that names the columns in the
    /// order <see cref="WriteRowAsync"/> writes them.
    /// </summary>
    protected abstract string CopyStatement { get; }

    /// <summary>
    /// Builds the row of an event.
    /// </summary>
    /// <param name="logEvent">Event to store.</param>
    /// <returns>The row to write.</returns>
    protected abstract TRow ToRow(LogEvent logEvent);

    /// <summary>
    /// Gets the UTC instant of a row, which chooses its partition.
    /// </summary>
    /// <param name="row">Row to place.</param>
    /// <returns>The instant of the row.</returns>
    protected abstract DateTimeOffset OccurredAtOf(TRow row);

    /// <summary>
    /// Writes the columns of one row, in the order of <see cref="CopyStatement"/>.
    /// </summary>
    /// <param name="importer">Binary import in progress.</param>
    /// <param name="row">Row to write.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task WriteRowAsync(NpgsqlBinaryImporter importer, TRow row);

    /// <summary>
    /// Writes one column value, or a null when it is missing.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="importer">Binary import in progress.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="type">PostgreSQL type of the column.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected static Task WriteAsync<T>(NpgsqlBinaryImporter importer, T? value, NpgsqlDbType type)
    {
        ArgumentNullException.ThrowIfNull(importer);

        return value is null ? importer.WriteNullAsync() : importer.WriteAsync(value, type);
    }

    /// <summary>
    /// Stores a batch of events.
    /// </summary>
    /// <param name="batch">Events to store.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var rows = Rows(batch);
        if (rows.Count is 0)
        {
            return;
        }

        await using var connection = await database.OpenConnectionAsync();
        await PrepareDaysAsync(connection, rows);
        try
        {
            await CopyAsync(connection, rows);
            return;
        }
        catch (PostgresException ex) when (ex.SqlState is NoPartitionForRow)
        {
            if (!await partitions.EnsurePartitionsAsync(connection, table, DaysOf(rows)))
            {
                await CopyEachAsync(connection, rows);
                return;
            }
        }
        catch (PostgresException ex) when (IsRefusedData(ex))
        {
            await CopyEachAsync(connection, rows);
            return;
        }

        try
        {
            await CopyAsync(connection, rows);
        }
        catch (PostgresException ex) when (IsRefusedData(ex))
        {
            await CopyEachAsync(connection, rows);
        }
    }

    /// <summary>
    /// Does nothing: the sink holds no state between batches.
    /// </summary>
    /// <returns>A completed task.</returns>
    public Task OnEmptyBatchAsync()
    {
        return Task.CompletedTask;
    }

    private List<TRow> Rows(IReadOnlyCollection<LogEvent> batch)
    {
        var rows = new List<TRow>(batch.Count);
        foreach (var logEvent in batch)
        {
            try
            {
                rows.Add(ToRow(logEvent));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                SelfLog.WriteLine(
                    "Dropped a {0} event that could not be converted: {1}",
                    table.Name,
                    ex.GetType()
                );
            }
        }

        return rows;
    }

    private IEnumerable<DateOnly> DaysOf(List<TRow> rows)
    {
        return rows.Select(row => LogPartitions.DayOf(OccurredAtOf(row)));
    }

    private async Task PrepareDaysAsync(NpgsqlConnection connection, List<TRow> rows)
    {
        var days = DaysOf(rows).Except(preparedDays).ToList();
        if (days.Count is 0)
        {
            return;
        }

        await partitions.EnsurePartitionsAsync(connection, table, days);
        preparedDays.UnionWith(days);
    }

    private async Task CopyEachAsync(NpgsqlConnection connection, List<TRow> rows)
    {
        foreach (var row in rows)
        {
            try
            {
                await CopyAsync(connection, [row]);
            }
            catch (PostgresException ex) when (IsRefusedData(ex))
            {
                SelfLog.WriteLine(
                    "Dropped a {0} row the database refused with {1}",
                    table.Name,
                    ex.SqlState
                );
            }
        }
    }

    private async Task CopyAsync(NpgsqlConnection connection, IReadOnlyList<TRow> rows)
    {
        await using var importer = await connection.BeginBinaryImportAsync(CopyStatement);
        foreach (var row in rows)
        {
            await importer.StartRowAsync();
            await WriteRowAsync(importer, row);
        }

        await importer.CompleteAsync();
    }

    private static bool IsRefusedData(PostgresException exception)
    {
        return exception.SqlState.StartsWith(DataExceptionClass, StringComparison.Ordinal)
            || exception.SqlState.StartsWith(IntegrityViolationClass, StringComparison.Ordinal);
    }
}
