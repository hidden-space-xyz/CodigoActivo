using Npgsql;
using NpgsqlTypes;
using Serilog.Events;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Stores technical log events in <c>diagnostic_log_entries</c>.
/// </summary>
/// <param name="database">Connections of the log tables, owned by the host.</param>
/// <param name="partitions">Maintenance of the log partitions.</param>
public sealed class DiagnosticLogSink(LogDatabase database, LogPartitionMaintainer partitions)
    : PartitionedLogSink<DiagnosticLogEntry>(database, partitions, LogTable.Diagnostics)
{
    /// <inheritdoc />
    protected override string CopyStatement =>
        "COPY diagnostic_log_entries (id, occurred_at, level, category, event_name, message, exception, trace_id, properties) FROM STDIN (FORMAT BINARY)";

    /// <inheritdoc />
    protected override DiagnosticLogEntry ToRow(LogEvent logEvent)
    {
        return DiagnosticLogEvents.ToEntry(logEvent);
    }

    /// <inheritdoc />
    protected override DateTimeOffset OccurredAtOf(DiagnosticLogEntry row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.OccurredAt;
    }

    /// <inheritdoc />
    protected override async Task WriteRowAsync(
        NpgsqlBinaryImporter importer,
        DiagnosticLogEntry row
    )
    {
        ArgumentNullException.ThrowIfNull(importer);
        ArgumentNullException.ThrowIfNull(row);

        await importer.WriteAsync(row.Id, NpgsqlDbType.Uuid);
        await importer.WriteAsync(row.OccurredAt, NpgsqlDbType.TimestampTz);
        await importer.WriteAsync(row.Level, NpgsqlDbType.Smallint);
        await importer.WriteAsync(row.Category, NpgsqlDbType.Varchar);
        await WriteAsync(importer, row.EventName, NpgsqlDbType.Varchar);
        await importer.WriteAsync(row.Message, NpgsqlDbType.Varchar);
        await WriteAsync(importer, row.Exception, NpgsqlDbType.Varchar);
        await WriteAsync(importer, row.TraceId, NpgsqlDbType.Varchar);
        await WriteAsync(importer, row.Properties, NpgsqlDbType.Jsonb);
    }
}
