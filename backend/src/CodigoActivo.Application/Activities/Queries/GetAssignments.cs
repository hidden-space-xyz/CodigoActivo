using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to retrieve the assignments of several users to one activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserIds">Identifiers of the assigned users, in the order the response keeps.</param>
public sealed record GetAssignmentsQuery(ActivityId ActivityId, IReadOnlyList<UserId> UserIds)
    : IQuery<IReadOnlyList<AssignmentResponse>>;

/// <summary>
/// Executes the query to retrieve the assignments of several users to one activity.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetAssignmentsQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetAssignmentsQuery, IReadOnlyList<AssignmentResponse>>
{
    /// <summary>
    /// Handles the request to retrieve the assignments of several users to one activity.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the existing assignments in the requested user order.</returns>
    public async Task<IReadOnlyList<AssignmentResponse>> HandleAsync(
        GetAssignmentsQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var activityId = query.ActivityId.Value;
        var userIds = query.UserIds.Select(userId => userId.Value).ToList();
        var found = await executor.ToListAsync(
            readStore
                .Assignments.Where(a => a.ActivityId == activityId && userIds.Contains(a.UserId))
                .Select(ActivityProjections.Assignment),
            ct
        );
        var byUser = found.ToDictionary(assignment => assignment.UserId);
        return [.. userIds.Where(byUser.ContainsKey).Select(userId => byUser[userId])];
    }
}
