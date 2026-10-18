using Npgsql;
using NpgsqlTypes;
using Serilog.Events;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Stores audited commands in <c>audit_log_entries</c>.
/// </summary>
/// <param name="database">Connections of the log tables, owned by the host.</param>
/// <param name="partitions">Maintenance of the log partitions.</param>
public sealed class AuditLogSink(LogDatabase database, LogPartitionMaintainer partitions)
    : PartitionedLogSink<AuditLogEntry>(database, partitions, LogTable.Audit)
{
    /// <inheritdoc />
    protected override string CopyStatement =>
        "COPY audit_log_entries (id, occurred_at, actor_type, actor_id, client_ip, action, entity_type, entity_id, related, outcome, error_code, trace_id) FROM STDIN (FORMAT BINARY)";

    /// <inheritdoc />
    protected override AuditLogEntry ToRow(LogEvent logEvent)
    {
        return AuditLogEvents.ToEntry(logEvent);
    }

    /// <inheritdoc />
    protected override DateTimeOffset OccurredAtOf(AuditLogEntry row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.OccurredAt;
    }

    /// <inheritdoc />
    protected override async Task WriteRowAsync(NpgsqlBinaryImporter importer, AuditLogEntry row)
    {
        ArgumentNullException.ThrowIfNull(importer);
        ArgumentNullException.ThrowIfNull(row);

        await importer.WriteAsync(row.Id, NpgsqlDbType.Uuid);
        await importer.WriteAsync(row.OccurredAt, NpgsqlDbType.TimestampTz);
        await importer.WriteAsync(row.ActorType, NpgsqlDbType.Varchar);
        await WriteAsync(importer, row.ActorId, NpgsqlDbType.Uuid);
        await WriteAsync(importer, row.ClientIp, NpgsqlDbType.Inet);
        await importer.WriteAsync(row.Action, NpgsqlDbType.Varchar);
        await WriteAsync(importer, row.EntityType, NpgsqlDbType.Varchar);
        await WriteAsync(importer, row.EntityId, NpgsqlDbType.Uuid);
        await WriteAsync(importer, row.Related, NpgsqlDbType.Jsonb);
        await importer.WriteAsync(row.Outcome, NpgsqlDbType.Varchar);
        await WriteAsync(importer, row.ErrorCode, NpgsqlDbType.Varchar);
        await WriteAsync(importer, row.TraceId, NpgsqlDbType.Varchar);
    }
}
