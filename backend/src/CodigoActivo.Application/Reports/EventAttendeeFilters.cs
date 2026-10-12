using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Reports.Contracts;

namespace CodigoActivo.Application.Reports;

/// <summary>
/// Narrows users to the attendees of an event that match the attendee list criteria.
/// </summary>
public static class EventAttendeeFilters
{
    /// <summary>
    /// Filters users to those attending the selected event.
    /// </summary>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <returns>The resulting user value.</returns>
    public static IQueryable<UserRow> Apply(
        IQueryable<UserRow> source,
        Guid eventId,
        EventAttendeeListQuery query
    )
    {
        var activityId = query.ActivityId;
        var roleTypeId = query.RoleTypeId;
        var statusId = query.StatusId;

        var matchingAssignments = source
            .SelectMany(user => user.Assignments)
            .Where(assignment => assignment.Activity.EventId == eventId);
        if (activityId is { } selectedActivityId)
        {
            matchingAssignments = matchingAssignments.Where(assignment =>
                assignment.ActivityId == selectedActivityId
            );
        }

        if (roleTypeId is { } selectedRoleTypeId)
        {
            matchingAssignments = matchingAssignments.Where(assignment =>
                assignment.ActivityRoleTypeId == selectedRoleTypeId
            );
        }

        if (statusId is { } selectedStatusId)
        {
            matchingAssignments = matchingAssignments.Where(assignment =>
                assignment.AssignmentStatusId == selectedStatusId
            );
        }

        var attendeeIds = matchingAssignments.Select(assignment => assignment.UserId).Distinct();
        source = source.Where(user => attendeeIds.Contains(user.Id));

        if (query.UserTypeId is { } userTypeId)
        {
            source = source.Where(u => u.UserTypeId == userTypeId);
        }

        if (query.Gender is { } gender)
        {
            source = source.Where(u => u.Gender == gender);
        }

        return source.WhereContains(
            u =>
                u.FirstName
                + " "
                + u.LastName
                + " "
                + (u.Email ?? "")
                + " "
                + (u.Phone ?? "")
                + " "
                + (u.SecondaryPhone ?? "")
                + (
                    u.Parent == null
                        ? ""
                        : " "
                            + u.Parent.FirstName
                            + " "
                            + u.Parent.LastName
                            + " "
                            + (u.Parent.Email ?? "")
                            + " "
                            + (u.Parent.Phone ?? "")
                            + " "
                            + (u.Parent.SecondaryPhone ?? "")
                ),
            query.Search
        );
    }
}
