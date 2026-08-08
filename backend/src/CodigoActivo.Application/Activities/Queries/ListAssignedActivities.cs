using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Queries;

public sealed record ListAssignedActivitiesQuery(Guid UserId, Guid? EventId)
    : IQuery<IReadOnlyList<AssignedActivityResponse>>;

public sealed class ListAssignedActivitiesQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor
) : IQueryHandler<ListAssignedActivitiesQuery, IReadOnlyList<AssignedActivityResponse>>
{
    public async Task<IReadOnlyList<AssignedActivityResponse>> HandleAsync(
        ListAssignedActivitiesQuery query,
        CancellationToken ct = default
    )
    {
        var source = activities
            .QueryAssignments()
            .Where(assignment => assignment.UserId == query.UserId)
            .Select(Projections.AssignedActivity);

        if (query.EventId is { } filterEventId)
        {
            source = source.Where(assignment => assignment.EventId == filterEventId);
        }

        return await executor.ToListAsync(
            source.OrderBy(assignment => assignment.ActivityStartsAt),
            ct
        );
    }
}
