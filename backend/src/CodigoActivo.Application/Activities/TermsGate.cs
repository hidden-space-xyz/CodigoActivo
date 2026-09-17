using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Evaluates whether the terms decisions supplied for a signup satisfy the business rules.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class TermsGate(
    IActivityRepository activities,
    IEventRepository events,
    IQueryExecutor executor,
    IClock clock
)
{
    /// <summary>
    /// Ensures that every terms document required by the activity's event has been accepted by
    /// the user. An acceptance is immutable: once recorded it is the proof of consent and is
    /// never overwritten or asked again. A rejection is revisable only by a decision that changes
    /// it to an acceptance: that later decision updates the same row in place instead of leaving
    /// the user permanently excluded (this also covers a document that was optional, got
    /// rejected, and was later made required by an administrator). A repeated rejection of an
    /// already-rejected document is not a change, so it leaves the row (and its original
    /// <c>DecidedAt</c>) untouched rather than stamping a needless update. Required documents
    /// never persist a rejection, neither as a new row nor as an update to an existing rejected
    /// row: the document is simply left undecided so a later call can still accept it. Decisions
    /// about documents that are not linked to the event are ignored. Nothing is persisted by this
    /// method: callers must call <see cref="IUnitOfWork.SaveChangesAsync"/> only after every
    /// other check in the same use case also succeeds.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="decisions">The terms decisions supplied by the caller, if any.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> EnsureDecidedAsync(
        Guid activityId,
        Guid userId,
        IReadOnlyList<TermsDecisionRequest>? decisions,
        CancellationToken ct
    )
    {
        var eventId = await executor.FirstOrDefaultAsync(
            activities.Query().Where(a => a.Id == activityId).Select(a => (Guid?)a.EventId),
            ct
        );
        if (eventId is null)
        {
            return Error.NotFound(ErrorCode.ActivityNotFound);
        }

        var documents = await executor.ToListAsync(
            events
                .QueryTermsDocuments()
                .Where(d => d.EventId == eventId)
                .Select(d => new { d.TermsDocumentId, d.IsRequired }),
            ct
        );
        if (documents.Count is 0)
        {
            return Result.Success();
        }

        var acceptances = await events.ListTermsAcceptancesAsync(eventId.Value, userId, ct);
        var acceptanceByDocument = acceptances.ToDictionary(a => a.TermsDocumentId);

        if (decisions is not null)
        {
            var requiredById = documents.ToDictionary(d => d.TermsDocumentId, d => d.IsRequired);
            foreach (var decision in decisions)
            {
                if (!requiredById.TryGetValue(decision.TermsDocumentId, out var isRequired))
                {
                    // Not linked to this event.
                    continue;
                }

                var accepted = decision.Accepted ?? false;

                if (acceptanceByDocument.TryGetValue(decision.TermsDocumentId, out var existing))
                {
                    if (existing.Accepted || !accepted)
                    {
                        // An acceptance is the proof of consent: immutable, never asked again.
                        // A rejection only changes when the incoming decision accepts instead: a
                        // repeated rejection is not a change, so the row (and its original
                        // DecidedAt) is left untouched instead of stamping a needless update. This
                        // also covers a required document whose stored decision is a rejection:
                        // it never persists another rejection, leaving it undecided so a later
                        // call can still accept it.
                        continue;
                    }

                    // The stored decision is a rejection that the caller is now accepting: update
                    // it in place instead of inserting a new row (the primary key would reject
                    // that anyway).
                    existing.Accepted = true;
                    existing.DecidedAt = clock.UtcNow;
                    continue;
                }

                if (isRequired && !accepted)
                {
                    // Same rule as above, for a document that has no decision at all yet.
                    continue;
                }

                var acceptance = new EventTermsAcceptance
                {
                    EventId = eventId.Value,
                    UserId = userId,
                    TermsDocumentId = decision.TermsDocumentId,
                    Accepted = accepted,
                    DecidedAt = clock.UtcNow,
                };
                await events.AddTermsAcceptanceAsync(acceptance, ct);
                acceptanceByDocument[decision.TermsDocumentId] = acceptance;
            }
        }

        var missingRequired = documents.Any(d =>
            d.IsRequired
            && (
                !acceptanceByDocument.TryGetValue(d.TermsDocumentId, out var acceptance)
                || !acceptance.Accepted
            )
        );
        return missingRequired
            ? (Result)Error.BadRequest(ErrorCode.EventTermsAcceptanceRequired)
            : Result.Success();
    }
}
