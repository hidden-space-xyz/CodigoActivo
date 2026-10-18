using CodigoActivo.Application.Abstractions.Auditing;

namespace CodigoActivo.Application.Logs.Contracts;

/// <summary>
/// Names the severity of a diagnostic log entry, stored as its number.
/// </summary>
public enum DiagnosticLevel
{
    /// <summary>Step-by-step tracing.</summary>
    Trace = 0,

    /// <summary>Debugging detail.</summary>
    Debug = 1,

    /// <summary>Normal operation.</summary>
    Information = 2,

    /// <summary>Something unexpected that did not fail.</summary>
    Warning = 3,

    /// <summary>A failure of one operation.</summary>
    Error = 4,

    /// <summary>A failure of the whole application.</summary>
    Critical = 5,
}

/// <summary>
/// Represents one diagnostic log entry in a list, without its exception.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
/// <param name="OccurredAt">UTC instant of the event.</param>
/// <param name="Level">Severity.</param>
/// <param name="Category">Logger category.</param>
/// <param name="EventName">Name of the declared event, if any.</param>
/// <param name="Message">Rendered message.</param>
/// <param name="HasException">Whether an exception is attached.</param>
/// <param name="TraceId">Trace the event was written in, if any.</param>
public sealed record DiagnosticLogEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    DiagnosticLevel Level,
    string Category,
    string? EventName,
    string Message,
    bool HasException,
    string? TraceId
);

/// <summary>
/// Represents one diagnostic log entry with its exception and the values of its message template.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
/// <param name="OccurredAt">UTC instant of the event.</param>
/// <param name="Level">Severity.</param>
/// <param name="Category">Logger category.</param>
/// <param name="EventName">Name of the declared event, if any.</param>
/// <param name="Message">Rendered message.</param>
/// <param name="Exception">Exception with its stack trace, if any.</param>
/// <param name="Properties">Values of the message template as a JSON object, if any.</param>
/// <param name="TraceId">Trace the event was written in, if any.</param>
public sealed record DiagnosticLogEntryDetailResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    DiagnosticLevel Level,
    string Category,
    string? EventName,
    string Message,
    string? Exception,
    string? Properties,
    string? TraceId
);

/// <summary>
/// Represents one audited command.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
/// <param name="OccurredAt">UTC instant the command ended.</param>
/// <param name="ActorType">Who ran it.</param>
/// <param name="ActorId">Account that ran it, if a user did.</param>
/// <param name="ActorName">Full name of that account, or null when it no longer exists.</param>
/// <param name="ActorEmail">Email of that account, if it still exists and has one.</param>
/// <param name="ClientIp">Address of the caller, if it came through a request.</param>
/// <param name="Action">Command that ran.</param>
/// <param name="EntityType">Kind of the main entity, if any.</param>
/// <param name="EntityId">Identifier of the main entity, if any.</param>
/// <param name="EntityName">Full name of the main entity when it is a user that still exists.</param>
/// <param name="Related">Other entities the command named.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="ErrorCode">Code of the error that rejected it, if any.</param>
/// <param name="TraceId">Trace it ran in, if any.</param>
public sealed record AuditLogEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditActorType ActorType,
    Guid? ActorId,
    string? ActorName,
    string? ActorEmail,
    string? ClientIp,
    AuditAction Action,
    AuditEntityType? EntityType,
    Guid? EntityId,
    string? EntityName,
    IReadOnlyList<AuditRelatedEntityResponse> Related,
    AuditOutcome Outcome,
    string? ErrorCode,
    string? TraceId
);

/// <summary>
/// Represents an entity an audited command named besides its main one.
/// </summary>
/// <param name="Type">Kind of the entity.</param>
/// <param name="Id">Identifier of the entity.</param>
public sealed record AuditRelatedEntityResponse(AuditEntityType Type, Guid Id);
