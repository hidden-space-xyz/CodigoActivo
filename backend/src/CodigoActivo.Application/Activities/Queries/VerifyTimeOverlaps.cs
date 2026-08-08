using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Queries;

public sealed record VerifyTimeOverlapsQuery(Guid ActivityId, Guid UserId)
    : IQuery<Result<TimeOverlapResponse>>;

public sealed class VerifyTimeOverlapsQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor
) : IQueryHandler<VerifyTimeOverlapsQuery, Result<TimeOverlapResponse>>
{
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
