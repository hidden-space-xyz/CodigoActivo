using System.Net;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Represents one command the application ran, stored in <c>audit_log_entries</c>: who ran it,
/// from where, on what and how it ended. The table is partitioned by day of
/// <see cref="OccurredAt"/> and only the audit sink writes it; this type exists to declare its
/// schema, so it never goes through a repository. <see cref="ActorId"/> and
/// <see cref="EntityId"/> have no foreign key, so the entries of an erased account stay until
/// their retention ends.
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    /// Identifies the maximum stored length of the enumerated columns.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Gets the version 7 identifier, which carries the instant it was created for.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the UTC instant the command ended, which also chooses its daily partition.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Gets who ran the command: a signed-in user, an anonymous caller or the system.
    /// </summary>
    public string ActorType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the account that ran the command, when a user was signed in.
    /// </summary>
    public Guid? ActorId { get; init; }

    /// <summary>
    /// Gets the address of the caller as the trusted proxy forwarded it.
    /// </summary>
    public IPAddress? ClientIp { get; init; }

    /// <summary>
    /// Gets the command that ran, named after it.
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Gets the kind of the main entity the command acted on.
    /// </summary>
    public string? EntityType { get; init; }

    /// <summary>
    /// Gets the identifier of the main entity the command acted on.
    /// </summary>
    public Guid? EntityId { get; init; }

    /// <summary>
    /// Gets the other entities the command named, as a JSON array of <c>{type, id}</c> objects.
    /// </summary>
    public string? Related { get; init; }

    /// <summary>
    /// Gets the flags and choices of the command, as a JSON object.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Gets how the command ended.
    /// </summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>
    /// Gets the code of the error that rejected the command.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets how many items the command changed, when it reports a count.
    /// </summary>
    public int? ItemCount { get; init; }

    /// <summary>
    /// Gets the trace id of the request the command ran in.
    /// </summary>
    public string? TraceId { get; init; }
}
