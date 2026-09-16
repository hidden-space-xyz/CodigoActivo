using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to list assigned activities.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="EventId">Identifier of the event.</param>
public sealed record ListAssignedActivitiesQuery(Guid UserId, Guid? EventId)
    : IQuery<IReadOnlyList<AssignedActivityResponse>>;

/// <summary>
/// Executes the query to list assigned activities.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListAssignedActivitiesQueryHandler(
    IActivityRepository activities,
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
