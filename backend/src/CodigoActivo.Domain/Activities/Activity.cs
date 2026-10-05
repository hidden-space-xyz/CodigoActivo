using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Activity of an event that people sign up to. It owns the desired number of people per role and
/// the signups: a person holds at most one signup per activity, which starts as requested and is
/// later confirmed or denied.
/// </summary>
public class Activity : AuditableEntity, IAggregateRoot
{
    private readonly List<ActivityRoleCapacity> roleCapacities = [];
    private readonly List<Assignment> assignments = [];

    private Activity() { }

    /// <summary>
    /// Gets the title displayed to users.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the description.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Gets where the activity takes place.
    /// </summary>
    public string Location { get; private set; } = string.Empty;

    /// <summary>
    /// Gets when the activity starts.
    /// </summary>
    public DateTimeOffset ActivityStartsAt { get; private set; }

    /// <summary>
    /// Gets when the activity ends.
    /// </summary>
    public DateTimeOffset ActivityEndsAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the event the activity belongs to.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Gets the identifier of the modality.
    /// </summary>
    public Guid ActivityModalityTypeId { get; private set; }

    /// <summary>
    /// Gets the identifier of the thumbnail file.
    /// </summary>
    public Guid ThumbnailId { get; private set; }

    /// <summary>
    /// Gets the desired number of people per role.
    /// </summary>
    public IReadOnlyCollection<ActivityRoleCapacity> RoleCapacities => roleCapacities;

    /// <summary>
    /// Gets the signups of the activity.
    /// </summary>
    public IReadOnlyCollection<Assignment> Assignments => assignments;

    /// <summary>
    /// Creates an activity in an event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="details">Titles, place, modality and thumbnail.</param>
    /// <param name="schedule">When the activity takes place.</param>
    /// <param name="capacities">Desired number of people per role.</param>
    /// <param name="authorId">Identifier of the user who creates it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The new activity.</returns>
    public static Activity Create(
        Guid eventId,
        ActivityDetails details,
        ActivitySchedule schedule,
        RoleCapacityPlan capacities,
        Guid authorId,
        DateTimeOffset now
    )
    {
        var activity = new Activity { EventId = eventId };
        activity.Apply(details, schedule, capacities);
        activity.RecordCreation(authorId, now);
        return activity;
    }

    /// <summary>
    /// Replaces the activity, keeping its signups.
    /// </summary>
    /// <param name="details">New titles, place, modality and thumbnail.</param>
    /// <param name="schedule">When the activity takes place from now on.</param>
    /// <param name="capacities">Desired number of people per role from now on.</param>
    /// <param name="editorId">Identifier of the user who edits it.</param>
    /// <param name="now">Current time.</param>
    public void Update(
        ActivityDetails details,
        ActivitySchedule schedule,
        RoleCapacityPlan capacities,
        Guid editorId,
        DateTimeOffset now
    )
    {
        Apply(details, schedule, capacities);
        RecordUpdate(editorId, now);
    }

    /// <summary>
    /// Tells whether the activity has already begun, so people can no longer join or leave it.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns><see langword="true"/> from the moment the activity starts.</returns>
    public bool HasStartedBy(DateTimeOffset now)
    {
        return now >= ActivityStartsAt;
    }

    /// <summary>
    /// Finds the signup of a person.
    /// </summary>
    /// <param name="userId">Identifier of the person.</param>
    /// <returns>The signup, or <see langword="null"/> when the person is not signed up.</returns>
    public Assignment? AssignmentOf(Guid userId)
    {
        return assignments.FirstOrDefault(assignment => assignment.UserId == userId);
    }

    /// <summary>
    /// Signs a person up with a role, pending a decision.
    /// </summary>
    /// <param name="userId">Identifier of the person.</param>
    /// <param name="roleTypeId">Role asked for.</param>
    /// <param name="now">Current time.</param>
    /// <returns>Success, or a conflict when the person is already signed up.</returns>
    public Result RequestAssignment(Guid userId, Guid roleTypeId, DateTimeOffset now)
    {
        if (AssignmentOf(userId) is not null)
        {
            return Error.Conflict(ErrorCode.ActivityAssignmentAlreadyExists);
        }

        assignments.Add(
            new Assignment(userId, Id, roleTypeId, SeedIds.AssignmentStatusTypes.Requested, now)
        );
        return Result.Success();
    }

    /// <summary>
    /// Removes the signup of a person.
    /// </summary>
    /// <param name="userId">Identifier of the person.</param>
    /// <returns>Success, or not found when the person is not signed up.</returns>
    public Result Unassign(Guid userId)
    {
        var assignment = AssignmentOf(userId);
        if (assignment is null)
        {
            return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);
        }

        assignments.Remove(assignment);
        return Result.Success();
    }

    /// <summary>
    /// Moves the signup of a person to another role, keeping its status and date.
    /// </summary>
    /// <param name="userId">Identifier of the signed-up person.</param>
    /// <param name="roleTypeId">New role.</param>
    /// <returns><see langword="true"/> when the role changed.</returns>
    public bool ChangeAssignmentRole(Guid userId, Guid roleTypeId)
    {
        var assignment =
            AssignmentOf(userId)
            ?? throw new InvalidOperationException("The person is not signed up.");
        if (assignment.ActivityRoleTypeId == roleTypeId)
        {
            return false;
        }

        assignments.Remove(assignment);
        assignments.Add(
            new Assignment(
                userId,
                Id,
                roleTypeId,
                assignment.AssignmentStatusId,
                assignment.CreatedAt
            )
        );
        return true;
    }

    /// <summary>
    /// Changes the status of the signup of a person.
    /// </summary>
    /// <param name="userId">Identifier of the signed-up person.</param>
    /// <param name="statusId">New status.</param>
    /// <returns>The status the signup had before.</returns>
    public Guid ChangeAssignmentStatus(Guid userId, Guid statusId)
    {
        var assignment =
            AssignmentOf(userId)
            ?? throw new InvalidOperationException("The person is not signed up.");
        var previousStatusId = assignment.AssignmentStatusId;
        assignment.ChangeStatus(statusId);
        return previousStatusId;
    }

    private void Apply(
        ActivityDetails details,
        ActivitySchedule schedule,
        RoleCapacityPlan capacities
    )
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(capacities);

        Title = details.Title.Trim();
        Description = details.Description;
        Location = details.Location.Trim();
        ActivityModalityTypeId = details.ActivityModalityTypeId;
        ThumbnailId = details.ThumbnailId;
        ActivityStartsAt = schedule.StartsAt;
        ActivityEndsAt = schedule.EndsAt;
        SyncRoleCapacities(capacities);
    }

    private void SyncRoleCapacities(RoleCapacityPlan plan)
    {
        var desiredByRole = plan.Items.ToDictionary(
            item => item.ActivityRoleTypeId,
            item => item.DesiredCount
        );
        roleCapacities.RemoveAll(capacity =>
            !desiredByRole.ContainsKey(capacity.ActivityRoleTypeId)
        );

        foreach (var (roleTypeId, desiredCount) in desiredByRole)
        {
            var existing = roleCapacities.FirstOrDefault(capacity =>
                capacity.ActivityRoleTypeId == roleTypeId
            );
            if (existing is null)
            {
                roleCapacities.Add(new ActivityRoleCapacity(Id, roleTypeId, desiredCount));
            }
            else
            {
                existing.Resize(desiredCount);
            }
        }
    }
}
