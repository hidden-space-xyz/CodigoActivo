using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities;

public sealed class TermsGate(
    IActivityRepository activities,
    IEventRepository events,
    IQueryExecutor executor,
    IClock clock
)
{
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
