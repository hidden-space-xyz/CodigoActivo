using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Common.Messaging;

/// <summary>
/// Describes what an audit entry records, before <see cref="AuditWriter"/> adds who caused it and
/// from where.
/// </summary>
/// <param name="Action">What happened.</param>
/// <param name="Outcome">How it ended.</param>
public sealed record AuditDraft(AuditAction Action, AuditOutcome Outcome)
{
    /// <summary>
    /// Gets the command, query or domain event whose typed identifiers the entry names.
    /// </summary>
    public object? Message { get; init; }

    /// <summary>
    /// Gets the main thing, when a handler named it.
    /// </summary>
    public AuditEntity? Subject { get; init; }

    /// <summary>
    /// Gets the value of the successful result, whose typed identifier is the main thing when no
    /// handler named one.
    /// </summary>
    public object? ResultValue { get; init; }

    /// <summary>
    /// Gets the name of the error code that refused it.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the application ran it on its own schedule.
    /// </summary>
    public bool Scheduled { get; init; }
}

/// <summary>
/// Writes audit entries with the signed-in user and the address of the request. A failure to record
/// is logged and never reaches the caller, so auditing cannot change what it records.
/// </summary>
/// <param name="trail">Audit log the entries are queued to.</param>
/// <param name="currentUser">Signed-in user, if any.</param>
/// <param name="origin">Origin of the request, if any.</param>
/// <param name="logger">Logger of entries that could not be recorded.</param>
public sealed class AuditWriter(
    IAuditTrail trail,
    ICurrentUser currentUser,
    IRequestOrigin origin,
    ILogger<AuditWriter> logger
)
{
    /// <summary>
    /// Queues one entry. The main thing is the one the draft names, else the one its result
    /// created, else the first typed identifier of its message; every other identifier is related.
    /// </summary>
    /// <param name="draft">What the entry records.</param>
    public void Write(AuditDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        try
        {
            IReadOnlyList<AuditEntity> named = draft.Message is { } message
                ? MessageAudit.EntitiesOf(message)
                : [];
            var entity =
                draft.Subject
                ?? MessageAudit.EntityOfResult(draft.ResultValue)
                ?? (named.Count > 0 ? named[0] : null);
            var actor = currentUser.Id;
            trail.Record(
                new AuditRecord(
                    draft.Action,
                    actor is not null ? AuditActorType.User
                        : draft.Scheduled ? AuditActorType.System
                        : AuditActorType.Anonymous,
                    actor,
                    origin.ClientAddress,
                    entity,
                    [.. named.Where(related => related != entity)],
                    draft.Outcome,
                    draft.ErrorCode
                )
            );
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.AuditEntryLost(draft.Action.ToString(), ex);
        }
    }
}
