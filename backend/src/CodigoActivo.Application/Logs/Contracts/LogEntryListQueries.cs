using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Logs.Contracts;

/// <summary>
/// Selects diagnostic log entries; every set criterion narrows the list. Sorts by
/// <c>occurredAt</c> (newest first by default) or <c>level</c>.
/// </summary>
public sealed class DiagnosticLogEntryListQuery : PageQuery
{
    /// <summary>
    /// Identifies the pattern a trace id filter must match.
    /// </summary>
    public const string TracePattern = "^[0-9a-fA-F]{32}$";

    /// <summary>
    /// Gets or sets a text the message must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the level the entries must have.
    /// </summary>
    [EnumDataType(typeof(DiagnosticLevel))]
    public DiagnosticLevel? Level { get; set; }

    /// <summary>
    /// Gets or sets a text the category or the event name must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the trace the entries were written in.
    /// </summary>
    [RegularExpression(TracePattern)]
    public string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the first local day the entries may have occurred on.
    /// </summary>
    public DateOnly? OccurredFrom { get; set; }

    /// <summary>
    /// Gets or sets the last local day the entries may have occurred on.
    /// </summary>
    public DateOnly? OccurredTo { get; set; }
}

/// <summary>
/// Selects audit log entries; every set criterion narrows the list. Sorts by <c>occurredAt</c>
/// (newest first by default) or <c>action</c>.
/// </summary>
public sealed class AuditLogEntryListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets a text the name or email of the actor, or of the user the entry is about, must
    /// contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets who must have run the commands.
    /// </summary>
    [EnumDataType(typeof(AuditActorType))]
    public AuditActorType? ActorType { get; set; }

    /// <summary>
    /// Gets or sets the account that must have run the commands.
    /// </summary>
    public Guid? ActorId { get; set; }

    /// <summary>
    /// Gets or sets the command the entries must record.
    /// </summary>
    [EnumDataType(typeof(AuditAction))]
    public AuditAction? Action { get; set; }

    /// <summary>
    /// Gets or sets the kind of the main entity of the entries.
    /// </summary>
    [EnumDataType(typeof(AuditEntityType))]
    public AuditEntityType? EntityType { get; set; }

    /// <summary>
    /// Gets or sets an entity the entries must be about or name among their related entities.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Gets or sets how the commands must have ended.
    /// </summary>
    [EnumDataType(typeof(AuditOutcome))]
    public AuditOutcome? Outcome { get; set; }

    /// <summary>
    /// Gets or sets the address the commands must have come from.
    /// </summary>
    [IpAddress]
    public string? ClientIp { get; set; }

    /// <summary>
    /// Gets or sets the trace the commands ran in.
    /// </summary>
    [RegularExpression(DiagnosticLogEntryListQuery.TracePattern)]
    public string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the first local day the entries may have occurred on.
    /// </summary>
    public DateOnly? OccurredFrom { get; set; }

    /// <summary>
    /// Gets or sets the last local day the entries may have occurred on.
    /// </summary>
    public DateOnly? OccurredTo { get; set; }
}
