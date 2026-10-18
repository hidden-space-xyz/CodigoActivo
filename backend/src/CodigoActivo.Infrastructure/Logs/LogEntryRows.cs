using System.Net;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Logs.Contracts;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Read row of <c>diagnostic_log_entries</c>, with the level as its enum.
/// </summary>
public sealed class DiagnosticLogRow
{
    /// <summary>
    /// Gets the identifier of the entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the UTC instant of the event.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Gets the severity.
    /// </summary>
    public DiagnosticLevel Level { get; init; }

    /// <summary>
    /// Gets the logger category.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the declared event, if any.
    /// </summary>
    public string? EventName { get; init; }

    /// <summary>
    /// Gets the rendered message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exception with its stack trace, if any.
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Gets the trace the event was written in, if any.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Gets the values of the message template as a JSON object, if any.
    /// </summary>
    public string? Properties { get; init; }
}

/// <summary>
/// Read row of <c>audit_log_entries</c>, with its enumerated columns as their enums.
/// </summary>
public sealed class AuditLogRow
{
    /// <summary>
    /// Gets the identifier of the entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the UTC instant the command ended.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Gets who ran the command.
    /// </summary>
    public AuditActorType ActorType { get; init; }

    /// <summary>
    /// Gets the account that ran the command, if a user did.
    /// </summary>
    public Guid? ActorId { get; init; }

    /// <summary>
    /// Gets the address of the caller, if any.
    /// </summary>
    public IPAddress? ClientIp { get; init; }

    /// <summary>
    /// Gets the command that ran.
    /// </summary>
    public AuditAction Action { get; init; }

    /// <summary>
    /// Gets the kind of the main entity, if any.
    /// </summary>
    public AuditEntityType? EntityType { get; init; }

    /// <summary>
    /// Gets the identifier of the main entity, if any.
    /// </summary>
    public Guid? EntityId { get; init; }

    /// <summary>
    /// Gets the other entities as a JSON array of <c>{type, id}</c> objects, if any.
    /// </summary>
    public string? Related { get; init; }

    /// <summary>
    /// Gets how the command ended.
    /// </summary>
    public AuditOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the code of the error that rejected the command, if any.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets the trace the command ran in, if any.
    /// </summary>
    public string? TraceId { get; init; }
}
