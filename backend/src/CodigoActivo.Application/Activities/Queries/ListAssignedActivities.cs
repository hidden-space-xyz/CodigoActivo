using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to list assigned activities.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="EventId">Identifier of the event.</param>
public sealed record ListAssignedActivitiesQuery(UserId UserId, EventId? EventId)
    : IQuery<IReadOnlyList<AssignedActivityResponse>>;

/// <summary>
/// Executes the query to list assigned activities.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListAssignedActivitiesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<ListAssignedActivitiesQuery, IReadOnlyList<AssignedActivityResponse>>
{
    /// <summary>
    /// Handles the request to list assigned activities.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching assigned activity items.</returns>
    public async Task<IReadOnlyList<AssignedActivityResponse>> HandleAsync(
        ListAssignedActivitiesQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var userId = query.UserId.Value;
        var source = readStore
            .Assignments.Where(assignment => assignment.UserId == userId)
            .Select(ActivityProjections.AssignedActivity);

        if (query.EventId is { Value: var filterEventId })
        {
            source = source.Where(assignment => assignment.EventId == filterEventId);
        }

        return await executor.ToListAsync(
            source.OrderBy(assignment => assignment.ActivityStartsAt),
            ct
        );
    }
}
