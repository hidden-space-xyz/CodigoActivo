using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Querying;

public static class UserFilters
{
    public static IQueryable<User> Apply(IQueryable<User> source, UserListQuery query)
    {
        if (query.Id is { } id)
        {
            source = source.Where(u => u.Id == id);
        }

        if (query.ParentId is { } parentId)
        {
            source = source.Where(u => u.ParentId == parentId);
        }

        if (query.UserTypeId is { } userTypeId)
        {
            source = source.Where(u => u.UserTypeId == userTypeId);
        }

        if (query.UserStatusTypeId is { } userStatusTypeId)
        {
            source = source.Where(u => u.UserStatusTypeId == userStatusTypeId);
        }

        if (query.IsAdmin is { } admin)
        {
            source = source.Where(u => u.IsAdmin == admin);
        }

        if (query.BirthDateFrom is { } birthDateFrom)
        {
            source = source.Where(u => u.BirthDate >= birthDateFrom);
        }

        if (query.BirthDateTo is { } birthDateTo)
        {
            source = source.Where(u => u.BirthDate <= birthDateTo);
        }

        source = source.WhereContains(u => u.FirstName + " " + u.LastName, query.Name);
        source = source.WhereContains(u => u.Email, query.Email);
        return source.WhereContains(u => u.Phone, query.Phone);
    }

    public static IQueryable<User> ApplyEventAttendees(
        IQueryable<User> source,
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
                ),
            query.Search
        );
    }
}
