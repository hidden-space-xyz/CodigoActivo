using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Evaluates whether terms is allowed by the business rules.
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
    /// Ensures that accepted satisfies the required business rules.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="acceptTerms">Whether accept terms.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> EnsureAcceptedAsync(
        Guid activityId,
        Guid userId,
        bool acceptTerms,
        CancellationToken ct
    )
    {
        var target = await executor.FirstOrDefaultAsync(
            activities
                .Query()
                .Where(a => a.Id == activityId)
                .Select(a => new TermsTarget(a.EventId, a.Event.TermsDocumentId)),
            ct
        );
        if (target is null)
        {
            return Error.NotFound(ErrorCode.ActivityNotFound);
        }

        if (target.TermsDocumentId is not { } termsDocumentId)
        {
            return Result.Success();
        }

        var acceptance = await events.GetTermsAcceptanceAsync(target.EventId, userId, ct);
        if (acceptance is not null && acceptance.TermsDocumentId == termsDocumentId)
        {
            return Result.Success();
        }

        if (!acceptTerms)
        {
            return Error.BadRequest(ErrorCode.EventTermsAcceptanceRequired);
        }

        if (acceptance is null)
        {
            await events.AddTermsAcceptanceAsync(
                new EventTermsAcceptance
                {
                    EventId = target.EventId,
                    UserId = userId,
                    TermsDocumentId = termsDocumentId,
                    AcceptedAt = clock.UtcNow,
                },
                ct
            );
        }
        else
        {
            acceptance.TermsDocumentId = termsDocumentId;
            acceptance.AcceptedAt = clock.UtcNow;
        }

        return Result.Success();
    }

    private sealed record TermsTarget(Guid EventId, Guid? TermsDocumentId);
}
