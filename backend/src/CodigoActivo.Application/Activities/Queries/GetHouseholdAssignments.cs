using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Queries;

public sealed record GetHouseholdAssignmentsQuery(Guid ActingUserId, Guid EventId)
    : IQuery<IReadOnlyList<HouseholdMemberAssignmentResponse>>;

public sealed class GetHouseholdAssignmentsQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor
) : IQueryHandler<GetHouseholdAssignmentsQuery, IReadOnlyList<HouseholdMemberAssignmentResponse>>
{
    public async Task<IReadOnlyList<HouseholdMemberAssignmentResponse>> HandleAsync(
        GetHouseholdAssignmentsQuery query,
        CancellationToken ct = default
    )
    {
        return await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(x =>
                    x.Activity.EventId == query.EventId
                    && (x.UserId == query.ActingUserId || x.User.ParentId == query.ActingUserId)
                )
                .OrderBy(x => x.User.FirstName)
                .ThenBy(x => x.User.LastName)
                .ThenBy(x => x.Activity.ActivityStartsAt)
                .ThenBy(x => x.ActivityId)
                .Select(x => new HouseholdMemberAssignmentResponse(
                    x.ActivityId,
                    x.UserId,
                    x.User.FirstName,
                    x.User.LastName,
                    x.ActivityRoleTypeId,
                    x.ActivityRoleType.Name,
                    x.AssignmentStatusId,
                    x.AssignmentStatus.Name
                )),
            ct
        );
    }
}
