using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to retrieve household assignments.
/// </summary>
/// <param name="ActingUserId">Identifier of the acting user.</param>
/// <param name="EventId">Identifier of the event.</param>
public sealed record GetHouseholdAssignmentsQuery(UserId ActingUserId, EventId EventId)
    : IQuery<IReadOnlyList<HouseholdMemberAssignmentResponse>>;

/// <summary>
/// Executes the query to retrieve household assignments.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetHouseholdAssignmentsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<GetHouseholdAssignmentsQuery, IReadOnlyList<HouseholdMemberAssignmentResponse>>
{
    /// <summary>
    /// Handles the request to retrieve household assignments.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching household member assignment items.</returns>
    public async Task<IReadOnlyList<HouseholdMemberAssignmentResponse>> HandleAsync(
        GetHouseholdAssignmentsQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var eventId = query.EventId.Value;
        var actingUserId = query.ActingUserId.Value;
        return await executor.ToListAsync(
            readStore
                .Assignments.Where(x =>
                    x.Activity.EventId == eventId
                    && (x.UserId == actingUserId || x.User.ParentId == actingUserId)
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
