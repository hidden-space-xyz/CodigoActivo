using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Reports.Queries;

public sealed record ListEventAttendeesQuery(Guid EventId, EventAttendeeListQuery Filters)
    : IQuery<PagedResult<EventAttendeeResponse>>;

public sealed class ListEventAttendeesQueryHandler(IUserRepository users, IQueryExecutor executor)
    : IQueryHandler<ListEventAttendeesQuery, PagedResult<EventAttendeeResponse>>
{
    private static readonly SortMap<User> AttendeeSort = new SortMap<User>()
        .Add("firstName", u => u.FirstName)
        .Add("lastName", u => u.LastName)
        .Add("email", u => u.Email)
        .Add("phone", u => u.Phone)
        .Add("birthDate", u => u.BirthDate)
        .Add("type", u => u.UserType.Name)
        .Add("createdAt", u => u.CreatedAt)
        .Default("firstName")
        .Tie(u => u.Id);

    public async Task<PagedResult<EventAttendeeResponse>> HandleAsync(
        ListEventAttendeesQuery query,
        CancellationToken ct = default
    )
    {
        var eventId = query.EventId;
        var filters = query.Filters;
        var activityId = filters.ActivityId;
        var roleTypeId = filters.RoleTypeId;
        var statusId = filters.StatusId;

        var source = UserFilters.ApplyEventAttendees(users.Query(), eventId, filters);

        var projected = AttendeeSort
            .Apply(source, filters.Sort)
            .Select(u => new AttendeeRow(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                u.BirthDate,
                u.Gender,
                u.UserType.Name,
                u.UserType.Color,
                u.Parent == null
                    ? null
                    : new EventAttendeeGuardianResponse(
                        u.Parent.FirstName,
                        u.Parent.LastName,
                        u.Parent.Email,
                        u.Parent.Phone
                    ),
                u.Assignments.Where(a =>
                        a.Activity.EventId == eventId
                        && (activityId == null || a.ActivityId == activityId)
                        && (roleTypeId == null || a.ActivityRoleTypeId == roleTypeId)
                        && (statusId == null || a.AssignmentStatusId == statusId)
                    )
                    .OrderBy(a => a.Activity.ActivityStartsAt)
                    .ThenBy(a => a.Activity.Title)
                    .Select(a => new EventAttendeeAssignmentResponse(
                        a.ActivityId,
                        a.Activity.Title,
                        a.Activity.ActivityStartsAt,
                        a.Activity.ActivityEndsAt,
                        a.ActivityRoleTypeId,
                        a.ActivityRoleType.Name,
                        a.AssignmentStatusId,
                        a.AssignmentStatus.Name,
                        a.CreatedAt,
                        false
                    ))
                    .ToList(),
                u.Assignments.Where(a =>
                        a.Activity.EventId == eventId
                        && a.AssignmentStatusId != SeedIds.AssignmentStatusTypes.Denied
                    )
                    .Select(a => new AssignmentWindow(
                        a.ActivityId,
                        a.Activity.ActivityStartsAt,
                        a.Activity.ActivityEndsAt
                    ))
                    .ToList()
            ));

        var page = await executor.ToPagedAsync(projected, filters.Page, filters.PageSize, ct);
        var items = page.Items.Select(ToAttendeeResponse).ToList();
        return new PagedResult<EventAttendeeResponse>(items, page.Total, page.Page, page.PageSize);
    }

    private static EventAttendeeResponse ToAttendeeResponse(AttendeeRow row)
    {
        var assignments = row.Assignments.ConvertAll(a =>
            a with
            {
                HasTimeConflict =
                    a.StatusId != SeedIds.AssignmentStatusTypes.Denied
                    && row.Windows.Exists(w =>
                        w.ActivityId != a.ActivityId
                        && a.ActivityStartsAt < w.EndsAt
                        && w.StartsAt < a.ActivityEndsAt
                    ),
            }
        );

        return new EventAttendeeResponse(
            row.UserId,
            row.FirstName,
            row.LastName,
            row.Email,
            row.Phone,
            row.BirthDate,
            row.Gender,
            row.UserTypeName,
            row.UserTypeColor,
            row.Guardian,
            assignments
        );
    }

    private sealed record AttendeeRow(
        Guid UserId,
        string? FirstName,
        string? LastName,
        string? Email,
        string? Phone,
        DateOnly BirthDate,
        Gender Gender,
        string UserTypeName,
        string UserTypeColor,
        EventAttendeeGuardianResponse? Guardian,
        List<EventAttendeeAssignmentResponse> Assignments,
        List<AssignmentWindow> Windows
    );

    private sealed record AssignmentWindow(
        Guid ActivityId,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt
    );
}
