using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the criteria used to list event attendees.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListEventAttendeesQuery(EventId EventId, EventAttendeeListQuery Filters)
    : IQuery<Result<PagedResult<EventAttendeeResponse>>>;

/// <summary>
/// Executes the query to list the people signed up to the activities of an event, each with the
/// signups that match the criteria and their guardian's contact details.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListEventAttendeesQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<ListEventAttendeesQuery, Result<PagedResult<EventAttendeeResponse>>>
{
    private static readonly SortMap<UserRow> AttendeeSort = new SortMap<UserRow>()
        .Add("firstName", u => u.FirstName)
        .Add("lastName", u => u.LastName)
        .Add("email", u => u.Email)
        .Add("phone", u => u.Phone)
        .Add("birthDate", u => u.BirthDate)
        .Add("type", u => u.UserType.Name)
        .Add("createdAt", u => u.CreatedAt)
        .Default("firstName")
        .Tie(u => u.Id);

    /// <summary>
    /// Handles the request to list event attendees.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the requested page of attendees, by first name unless the sort
    /// names <c>lastName</c>, <c>email</c>, <c>phone</c>, <c>birthDate</c>, <c>type</c> or
    /// <c>createdAt</c>; or <see cref="ApplicationErrorCode.EventNotFound"/>.
    /// </returns>
    public async Task<Result<PagedResult<EventAttendeeResponse>>> HandleAsync(
        ListEventAttendeesQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var eventId = query.EventId.Value;
        if (!await executor.AnyAsync(readStore.Events.Where(e => e.Id == eventId), ct))
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var filters = query.Filters;
        var source = EventAttendeeFilters.Apply(readStore.Users, eventId, filters);

        var projected = AttendeeSort
            .Apply(source, filters.Sort)
            .Select(u => new AttendeeRow(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                u.SecondaryPhone,
                u.BirthDate,
                u.Gender,
                u.UserType.Name,
                u.UserType.Color,
                u.Parent == null
                    ? null
                    : new EventGuardianContactResponse(
                        u.Parent.FirstName,
                        u.Parent.LastName,
                        u.Parent.Email,
                        u.Parent.Phone,
                        u.Parent.SecondaryPhone
                    ),
                u.Assignments.AsQueryable()
                    .Where(EventAttendeeFilters.MatchingAssignment(eventId, filters))
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
                        && a.AssignmentStatusId
                            != CatalogIds.AssignmentStatuses.IdOf(AssignmentStatus.Denied)
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
                    a.StatusId != CatalogIds.AssignmentStatuses.IdOf(AssignmentStatus.Denied)
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
            row.SecondaryPhone,
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
        string? SecondaryPhone,
        DateOnly? BirthDate,
        Gender Gender,
        string UserTypeName,
        string UserTypeColor,
        EventGuardianContactResponse? Guardian,
        List<EventAttendeeAssignmentResponse> Assignments,
        List<AssignmentWindow> Windows
    );

    private sealed record AssignmentWindow(
        Guid ActivityId,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt
    );
}
