using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to find which signups of a person overlap in time with an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity the person would sign up to.</param>
/// <param name="UserId">Identifier of the signed-in user or of someone they may act for.</param>
public sealed record VerifyTimeOverlapsQuery(ActivityId ActivityId, UserId UserId)
    : IQuery<Result<TimeOverlapResponse>>;

/// <summary>
/// Executes the query to find the other activities the person is signed up to, unless denied,
/// whose time range overlaps the activity.
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
    /// <returns>
    /// A task whose result contains the overlapping activities ordered by start;
    /// <see cref="ApplicationErrorCode.ActingForAnotherUserForbidden"/> when the user may not act
    /// for the person, or <see cref="ApplicationErrorCode.ActivityNotFound"/>.
    /// </returns>
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
                    && x.AssignmentStatusId
                        != CatalogIds.AssignmentStatuses.IdOf(AssignmentStatus.Denied)
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
