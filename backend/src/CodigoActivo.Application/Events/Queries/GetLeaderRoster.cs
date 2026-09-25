using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Queries;

/// <summary>
/// Carries the criteria used to retrieve the attendee lists of the activities that the requesting
/// user leads in an event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the requesting user.</param>
public sealed record GetLeaderRosterQuery(Guid EventId, Guid UserId)
    : IQuery<IReadOnlyList<LeaderRosterActivityResponse>>;

/// <summary>
/// Executes the query to retrieve the leader roster of an event. It returns only the activities of
/// the event that have not ended and in which the requesting user holds a confirmed leader
/// assignment of their own, each with its currently confirmed attendees; everyone else, including
/// administrators and the guardians of a leading minor, receives an empty list.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to exclude ended activities and to find each activity's local day.</param>
public sealed class GetLeaderRosterQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<GetLeaderRosterQuery, IReadOnlyList<LeaderRosterActivityResponse>>
{
    /// <summary>
    /// Handles the request to retrieve the leader roster of an event.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the led activities in chronological order.</returns>
    public async Task<IReadOnlyList<LeaderRosterActivityResponse>> HandleAsync(
        GetLeaderRosterQuery query,
        CancellationToken ct = default
    )
    {
        var now = clock.UtcNow;
        var ledActivities = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    a.UserId == query.UserId
                    && a.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    && a.Activity.EventId == query.EventId
                    && a.Activity.ActivityEndsAt > now
                )
                .Select(a => new LedActivity(
                    a.ActivityId,
                    a.Activity.Title,
                    a.Activity.Location,
                    a.Activity.ActivityStartsAt,
                    a.Activity.ActivityEndsAt
                )),
            ct
        );
        if (ledActivities.Count == 0)
        {
            return [];
        }

        var ledActivityIds = ledActivities.Select(a => a.ActivityId).ToList();
        var attendees = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    ledActivityIds.Contains(a.ActivityId)
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                )
                .Select(a => new AttendeeRow(
                    a.ActivityId,
                    a.ActivityRoleTypeId,
                    a.ActivityRoleType.Name,
                    a.UserId,
                    a.User.FirstName,
                    a.User.LastName,
                    a.User.Email,
                    a.User.Phone,
                    a.User.BirthDate,
                    a.User.Parent == null
                        ? null
                        : new LeaderRosterGuardianResponse(
                            a.User.Parent.FirstName,
                            a.User.Parent.LastName,
                            a.User.Parent.Email,
                            a.User.Parent.Phone
                        ),
                    a.CreatedAt
                )),
            ct
        );

        var attendeesByActivity = attendees.ToLookup(a => a.ActivityId);
        return
        [
            .. ledActivities
                .OrderBy(a => a.ActivityStartsAt)
                .ThenBy(a => a.Title, StringComparer.Ordinal)
                .ThenBy(a => a.ActivityId)
                .Select(a => new LeaderRosterActivityResponse(
                    a.ActivityId,
                    a.Title,
                    a.Location,
                    a.ActivityStartsAt,
                    a.ActivityEndsAt,
                    ToRoles(attendeesByActivity[a.ActivityId], LocalDay(a.ActivityStartsAt))
                )),
        ];
    }

    private DateOnly LocalDay(DateTimeOffset instant)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, clock.TimeZone).DateTime);
    }

    private static List<LeaderRosterRoleResponse> ToRoles(
        IEnumerable<AttendeeRow> rows,
        DateOnly activityDay
    )
    {
        return
        [
            .. rows.GroupBy(r => (r.RoleTypeId, r.RoleName))
                .OrderBy(g => ActivityRoleOrder.Of(g.Key.RoleTypeId))
                .ThenBy(g => g.Key.RoleName, StringComparer.Ordinal)
                .Select(g => ToRole(g.Key.RoleTypeId, g.Key.RoleName, g, activityDay)),
        ];
    }

    private static LeaderRosterRoleResponse ToRole(
        Guid roleTypeId,
        string roleName,
        IEnumerable<AttendeeRow> rows,
        DateOnly activityDay
    )
    {
        var users = new List<LeaderRosterUserResponse>();
        var dependents = new List<LeaderRosterDependentResponse>();
        foreach (
            var row in rows.OrderBy(r => TextSearch.Normalize(r.FirstName), StringComparer.Ordinal)
                .ThenBy(r => TextSearch.Normalize(r.LastName), StringComparer.Ordinal)
                .ThenBy(r => r.UserId)
        )
        {
            if (row.Guardian is { } guardian)
            {
                dependents.Add(
                    new LeaderRosterDependentResponse(
                        row.FirstName,
                        row.LastName,
                        row.BirthDate?.AgeOn(activityDay),
                        guardian,
                        row.SignedUpAt
                    )
                );
            }
            else
            {
                users.Add(
                    new LeaderRosterUserResponse(
                        row.FirstName,
                        row.LastName,
                        row.Email,
                        row.Phone,
                        row.SignedUpAt
                    )
                );
            }
        }

        return new LeaderRosterRoleResponse(roleTypeId, roleName, users, dependents);
    }

    private sealed record LedActivity(
        Guid ActivityId,
        string Title,
        string Location,
        DateTimeOffset ActivityStartsAt,
        DateTimeOffset ActivityEndsAt
    );

    private sealed record AttendeeRow(
        Guid ActivityId,
        Guid RoleTypeId,
        string RoleName,
        Guid UserId,
        string FirstName,
        string LastName,
        string? Email,
        string? Phone,
        DateOnly? BirthDate,
        LeaderRosterGuardianResponse? Guardian,
        DateTimeOffset SignedUpAt
    );
}
