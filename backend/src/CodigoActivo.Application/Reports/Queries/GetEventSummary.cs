using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the criteria used to retrieve event summary.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record GetEventSummaryQuery(Guid EventId) : IQuery<Result<EventSummaryResponse>>;

/// <summary>
/// Executes the query to retrieve event summary.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="roleTypes">Repository used to persist and retrieve role types.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventSummaryQueryHandler(
    IEventRepository events,
    IActivityRoleTypeRepository roleTypes,
    IActivityRepository activities,
    IQueryExecutor executor
) : IQueryHandler<GetEventSummaryQuery, Result<EventSummaryResponse>>
{
    /// <summary>
    /// Handles the request to retrieve event summary.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event summary on success, or an application error on failure.</returns>
    public async Task<Result<EventSummaryResponse>> HandleAsync(
        GetEventSummaryQuery query,
        CancellationToken ct = default
    )
    {
        var eventId = query.EventId;
        var ev = await executor.FirstOrDefaultAsync(
            events
                .Query()
                .Where(e => e.Id == eventId)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    ActivitiesCount = e.Activities.Count,
                    RatingsCount = e.Ratings.Count,
                    RatingsAverage = e.Ratings.Average(rating => (double?)rating.Score),
                }),
            ct
        );
        if (ev is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var stats = await executor.FirstOrDefaultAsync(
            activities
                .QueryAssignments()
                .Where(a => a.Activity.EventId == eventId)
                .GroupBy(a => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Requested = g.Count(a =>
                        a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Requested
                    ),
                    Confirmed = g.Count(a =>
                        a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    ),
                    Denied = g.Count(a =>
                        a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Denied
                    ),
                    DistinctUsers = g.Select(a => a.UserId).Distinct().Count(),
                }),
            ct
        );

        var roleTypeBreakdown = await executor.ToListAsync(
            roleTypes
                .Query()
                .OrderBy(role => role.Name)
                .Select(role => new EventRoleTypeSummaryResponse(
                    role.Id,
                    role.Name,
                    role.Assignments.Count(a =>
                        a.Activity.EventId == eventId
                        && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    )
                )),
            ct
        );

        return new EventSummaryResponse(
            ev.Id,
            ev.Title,
            ev.ActivitiesCount,
            stats?.Total ?? 0,
            stats?.Requested ?? 0,
            stats?.Confirmed ?? 0,
            stats?.Denied ?? 0,
            stats?.DistinctUsers ?? 0,
            ev.RatingsCount,
            ev.RatingsAverage,
            roleTypeBreakdown
        );
    }
}
