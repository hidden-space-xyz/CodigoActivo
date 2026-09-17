using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to retrieve the aggregated signup statistics for an event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the requesting user.</param>
/// <param name="IsAdmin">Whether the requesting user is an administrator.</param>
public sealed record GetEventSignupStatsQuery(Guid EventId, Guid UserId, bool IsAdmin)
    : IQuery<Result<EventSignupStatsResponse>>;

/// <summary>
/// Executes the query to retrieve the aggregated signup statistics for an event. Only members
/// and administrators may access this data; the check is resolved here by looking up the
/// requesting user's type instead of relying on a dedicated claim.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="roleTypes">Handler used to list activity role types.</param>
/// <param name="statusTypes">Handler used to list assignment status types.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventSignupStatsQueryHandler(
    IEventRepository events,
    IActivityRepository activities,
    IUserRepository users,
    ListActivityRoleTypesQueryHandler roleTypes,
    ListAssignmentStatusTypesQueryHandler statusTypes,
    IQueryExecutor executor
) : IQueryHandler<GetEventSignupStatsQuery, Result<EventSignupStatsResponse>>
{
    /// <summary>
    /// Handles the request to retrieve the aggregated signup statistics for an event.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the event's signup statistics on success, or an application error on failure.</returns>
    public async Task<Result<EventSignupStatsResponse>> HandleAsync(
        GetEventSignupStatsQuery query,
        CancellationToken ct = default
    )
    {
        if (!query.IsAdmin)
        {
            var userTypeId = await executor.FirstOrDefaultAsync(
                users.Query().Where(u => u.Id == query.UserId).Select(u => (Guid?)u.UserTypeId),
                ct
            );
            if (userTypeId != SeedIds.UserTypes.Member)
            {
                return Error.Forbidden(ErrorCode.AccessDenied);
            }
        }

        if (!await events.ExistsAsync(e => e.Id == query.EventId, ct))
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var eventActivities = await executor.ToListAsync(
            activities
                .Query()
                .Where(a => a.EventId == query.EventId)
                .OrderBy(a => a.ActivityStartsAt)
                .ThenBy(a => a.Title)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.ActivityStartsAt,
                }),
            ct
        );

        var cells = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(x => x.Activity.EventId == query.EventId)
                .GroupBy(x => new
                {
                    x.ActivityId,
                    x.ActivityRoleTypeId,
                    x.AssignmentStatusId,
                })
                .Select(g => new
                {
                    g.Key.ActivityId,
                    g.Key.ActivityRoleTypeId,
                    g.Key.AssignmentStatusId,
                    Count = g.Count(),
                }),
            ct
        );

        var cellsByActivity = cells
            .GroupBy(c => c.ActivityId)
            .ToDictionary(
                g => g.Key,
                g =>
                    (IReadOnlyList<EventSignupStatsCellResponse>)
                        g.Select(c => new EventSignupStatsCellResponse(
                                c.ActivityRoleTypeId,
                                c.AssignmentStatusId,
                                c.Count
                            ))
                            .ToList()
            );

        var activityResponses = eventActivities
            .Select(a => new EventSignupStatsActivityResponse(
                a.Id,
                a.Title,
                a.ActivityStartsAt,
                cellsByActivity.TryGetValue(a.Id, out var activityCells)
                    ? activityCells
                    : []
            ))
            .ToList();

        var roles = await roleTypes.HandleAsync(new ListActivityRoleTypesQuery(), ct);
        var statuses = await statusTypes.HandleAsync(new ListAssignmentStatusTypesQuery(), ct);

        var total = cells.Sum(c => c.Count);
        var requested = cells
            .Where(c => c.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Requested)
            .Sum(c => c.Count);
        var confirmed = cells
            .Where(c => c.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed)
            .Sum(c => c.Count);
        var denied = cells
            .Where(c => c.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Denied)
            .Sum(c => c.Count);

        return new EventSignupStatsResponse(
            query.EventId,
            roles.Select(r => new EventSignupStatsRoleResponse(r.Id, r.Name)).ToList(),
            statuses.Select(s => new EventSignupStatsStatusResponse(s.Id, s.Name)).ToList(),
            activityResponses,
            new EventSignupStatsTotalsResponse(total, requested, confirmed, denied)
        );
    }
}
