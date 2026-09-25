using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the criteria used to retrieve event roster.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record GetEventRosterQuery(Guid EventId) : IQuery<Result<EventRosterResponse>>;

/// <summary>
/// Executes the query to retrieve event roster.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventRosterQueryHandler(
    IEventRepository events,
    IActivityRepository activities,
    IQueryExecutor executor
) : IQueryHandler<GetEventRosterQuery, Result<EventRosterResponse>>
{
    /// <summary>
    /// Handles the request to retrieve event roster.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event roster on success, or an application error on failure.</returns>
    public async Task<Result<EventRosterResponse>> HandleAsync(
        GetEventRosterQuery query,
        CancellationToken ct = default
    )
    {
        var eventId = query.EventId;
        var ev = await GetEventHeaderAsync(eventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var rows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    a.Activity.EventId == eventId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                )
                .Select(a => new
                {
                    a.ActivityId,
                    ActivityTitle = a.Activity.Title,
                    a.Activity.Location,
                    a.Activity.ActivityStartsAt,
                    a.Activity.ActivityEndsAt,
                    a.UserId,
                    a.User.FirstName,
                    a.User.LastName,
                    a.User.BirthDate,
                    a.User.Email,
                    a.User.Phone,
                    a.User.SecondaryPhone,
                    a.ActivityRoleTypeId,
                    RoleName = a.ActivityRoleType.Name,
                    Guardian = a.User.Parent == null
                        ? null
                        : new EventRosterGuardianResponse(
                            a.User.Parent.FirstName,
                            a.User.Parent.LastName,
                            a.User.Parent.Email,
                            a.User.Parent.Phone,
                            a.User.Parent.SecondaryPhone
                        ),
                }),
            ct
        );

        var rosterActivities = rows.GroupBy(r => r.ActivityId)
            .Select(g =>
            {
                var activity = g.First();
                return new EventRosterActivityResponse(
                    g.Key,
                    activity.ActivityTitle,
                    activity.Location,
                    activity.ActivityStartsAt,
                    activity.ActivityEndsAt,
                    [
                        .. g.OrderBy(r => ActivityRoleOrder.Of(r.ActivityRoleTypeId))
                            .ThenBy(r => TextSearch.Normalize(r.FirstName), StringComparer.Ordinal)
                            .ThenBy(r => TextSearch.Normalize(r.LastName), StringComparer.Ordinal)
                            .ThenBy(r => r.UserId)
                            .DistinctBy(r => r.UserId)
                            .Select(r => new EventRosterParticipantResponse(
                                r.UserId,
                                r.FirstName,
                                r.LastName,
                                r.BirthDate,
                                r.Email,
                                r.Phone,
                                r.SecondaryPhone,
                                r.RoleName,
                                r.Guardian
                            )),
                    ]
                );
            })
            .OrderBy(a => a.ActivityStartsAt)
            .ThenBy(a => a.Title, StringComparer.Ordinal)
            .ThenBy(a => a.ActivityId)
            .ToList();

        return new EventRosterResponse(ev.Id, ev.Title, rosterActivities);
    }

    private Task<EventHeader?> GetEventHeaderAsync(Guid eventId, CancellationToken ct)
    {
        return executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == eventId).Select(e => new EventHeader(e.Id, e.Title)),
            ct
        );
    }

    private sealed record EventHeader(Guid Id, string Title);
}
