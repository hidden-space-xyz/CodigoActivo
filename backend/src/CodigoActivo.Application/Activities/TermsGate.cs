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
    /// the user, recording any newly supplied decision for a document that had none yet.
    /// Required documents only persist an accepted decision: rejecting a required document is
    /// never stored, so the user can retry and accept it on a later call instead of being
    /// permanently excluded from the event by a single mistaken click. Optional documents persist
    /// whichever decision the user makes, accepted or rejected, and never block the signup.
    /// Decisions about documents that are already decided (including a previously rejected
    /// optional document) or that are not linked to the event are ignored, which makes repeated
    /// calls idempotent. Nothing is persisted by this method: callers must call
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> only after every other check in the same use
    /// case also succeeds.
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
        var decidedById = acceptances.ToDictionary(a => a.TermsDocumentId, a => a.Accepted);

        if (decisions is not null)
        {
            var requiredById = documents.ToDictionary(d => d.TermsDocumentId, d => d.IsRequired);
            foreach (var decision in decisions)
            {
                if (
                    !requiredById.TryGetValue(decision.TermsDocumentId, out var isRequired)
                    || decidedById.ContainsKey(decision.TermsDocumentId)
                )
                {
                    continue;
                }

                var accepted = decision.Accepted ?? false;
                if (isRequired && !accepted)
                {
                    // A rejection of a required document is never stored: persisting it would
                    // make the document permanently undecided-as-rejected (decisions are
                    // immutable once recorded), locking the user out of the event forever with no
                    // screen to change their mind. Leaving it undecided lets a later call accept
                    // it instead.
                    continue;
                }

                await events.AddTermsAcceptanceAsync(
                    new EventTermsAcceptance
                    {
                        EventId = eventId.Value,
                        UserId = userId,
                        TermsDocumentId = decision.TermsDocumentId,
                        Accepted = accepted,
                        DecidedAt = clock.UtcNow,
                    },
                    ct
                );
                decidedById[decision.TermsDocumentId] = accepted;
            }
        }

        var missingRequired = documents.Any(d =>
            d.IsRequired
            && (!decidedById.TryGetValue(d.TermsDocumentId, out var accepted) || !accepted)
        );
        return missingRequired
            ? (Result)Error.BadRequest(ErrorCode.EventTermsAcceptanceRequired)
            : Result.Success();
    }
}
