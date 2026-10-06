using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to verify time overlaps.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record VerifyTimeOverlapsQuery(ActivityId ActivityId, UserId UserId)
    : IQuery<Result<TimeOverlapResponse>>;

/// <summary>
/// Executes the query to verify time overlaps.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="actingUser">Policy that decides for whom the signed-in user may act.</param>
public sealed class VerifyTimeOverlapsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    ActingUserPolicy actingUser
) : IQueryHandler<VerifyTimeOverlapsQuery, Result<TimeOverlapResponse>>
{
    /// <summary>
    /// Handles the request to verify time overlaps.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a time overlap on success, or an application error on failure.</returns>
    public async Task<Result<TimeOverlapResponse>> HandleAsync(
        VerifyTimeOverlapsQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var allowed = await actingUser.EnsureMayActForAsync(query.UserId, ct);
        if (allowed.IsFailure)
        {
            return allowed.Error!;
        }

        var activityId = query.ActivityId.Value;
        var userId = query.UserId.Value;
        var target = await executor.FirstOrDefaultAsync(
            readStore
                .Activities.Where(a => a.Id == activityId)
                .Select(a => new { a.ActivityStartsAt, a.ActivityEndsAt }),
            ct
        );
        if (target is null)
        {
            return Error.NotFound(ApplicationErrorCode.ActivityNotFound);
        }

        var overlaps = await executor.ToListAsync(
            readStore
                .Assignments.Where(x =>
                    x.UserId == userId
                    && x.ActivityId != activityId
                    && x.Activity.ActivityStartsAt < target.ActivityEndsAt
                    && target.ActivityStartsAt < x.Activity.ActivityEndsAt
                )
                .OrderBy(x => x.Activity.ActivityStartsAt)
                .ThenBy(x => x.ActivityId)
                .Select(x => new OverlappingActivityResponse(
                    x.ActivityId,
                    x.Activity.Title,
                    x.Activity.ActivityStartsAt,
                    x.Activity.ActivityEndsAt
                )),
            ct
        );

        return new TimeOverlapResponse(overlaps.Count > 0, overlaps);
    }
}
