using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to verify time overlaps.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record VerifyTimeOverlapsQuery(Guid ActivityId, Guid UserId)
    : IQuery<Result<TimeOverlapResponse>>;

/// <summary>
/// Executes the query to verify time overlaps.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class VerifyTimeOverlapsQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor
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
        var target = await executor.FirstOrDefaultAsync(
            activities
                .Query()
                .Where(a => a.Id == query.ActivityId)
                .Select(a => new { a.ActivityStartsAt, a.ActivityEndsAt }),
            ct
        );
        if (target is null)
        {
            return Error.NotFound(ErrorCode.ActivityNotFound);
        }

        var overlaps = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(x =>
                    x.UserId == query.UserId
                    && x.ActivityId != query.ActivityId
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
