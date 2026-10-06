using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Activity of an event that people sign up to. It owns the desired number of people per role and
/// the signups: a person holds at most one signup per activity, which starts as requested and is
/// later confirmed or denied.
/// </summary>
public class Activity : AuditableEntity<ActivityId>
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
    /// Gets when the activity starts and ends, in UTC.
    /// </summary>
    public ActivitySchedule Schedule =>
        ActivitySchedule.FromStored(ActivityStartsAt, ActivityEndsAt);

    /// <summary>
    /// Gets the identifier of the event the activity belongs to.
    /// </summary>
    public EventId EventId { get; private set; }

    /// <summary>
    /// Gets where the activity takes place.
    /// </summary>
    public ActivityModality Modality { get; private set; }

    /// <summary>
    /// Gets the identifier of the thumbnail file.
    /// </summary>
    public StoredFileId ThumbnailId { get; private set; }

    /// <summary>
    /// Gets the desired number of people per role.
    /// </summary>
    public IReadOnlyCollection<ActivityRoleCapacity> RoleCapacities => roleCapacities;

    /// <summary>
    /// Gets the signups of the activity.
    /// </summary>
    public IReadOnlyCollection<Assignment> Assignments => assignments;

    private DateTimeOffset ActivityStartsAt { get; set; }

    private DateTimeOffset ActivityEndsAt { get; set; }

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
        EventId eventId,
        ActivityDetails details,
        ActivitySchedule schedule,
        RoleCapacityPlan capacities,
        UserId authorId,
        DateTimeOffset now
    )
    {
        var activity = new Activity { EventId = eventId };
        activity.Apply(details, schedule, capacities);
        activity.RecordCreation(authorId, now);
        activity.Raise(new ActivityCreated(activity.Id));
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
        UserId editorId,
        DateTimeOffset now
    )
    {
        var previousThumbnailId = ThumbnailId;
        Apply(details, schedule, capacities);
        RecordUpdate(editorId, now);
        Raise(
            new ActivityUpdated(Id, previousThumbnailId == ThumbnailId ? [] : [previousThumbnailId])
        );
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
    public Assignment? AssignmentOf(UserId userId)
    {
        return assignments.FirstOrDefault(assignment => assignment.UserId == userId);
    }

    /// <summary>
    /// Signs a person up with a role, pending a decision.
    /// </summary>
    /// <param name="userId">Identifier of the person.</param>
    /// <param name="role">Role asked for.</param>
    /// <param name="now">Current time.</param>
    /// <returns>Success, or a conflict when the person is already signed up.</returns>
    public Result RequestAssignment(UserId userId, ActivityRole role, DateTimeOffset now)
    {
        if (AssignmentOf(userId) is not null)
        {
            return Error.Conflict(DomainErrorCode.ActivityAssignmentAlreadyExists);
        }

        assignments.Add(new Assignment(userId, Id, role, AssignmentStatus.Requested, now));
        Raise(new AssignmentRequested(Id, userId, role));
        return Result.Success();
    }

    /// <summary>
    /// Removes the signup of a person.
    /// </summary>
    /// <param name="userId">Identifier of the person.</param>
    /// <returns>Success, or not found when the person is not signed up.</returns>
    public Result Unassign(UserId userId)
    {
        var assignment = AssignmentOf(userId);
        if (assignment is null)
        {
            return Error.NotFound(DomainErrorCode.ActivityAssignmentNotFound);
        }

        assignments.Remove(assignment);
        Raise(new AssignmentWithdrawn(Id, userId));
        return Result.Success();
    }

    /// <summary>
    /// Moves the signup of a person to another role, keeping its status and date.
    /// </summary>
    /// <param name="userId">Identifier of the signed-up person.</param>
    /// <param name="role">New role.</param>
    /// <returns><see langword="true"/> when the role changed.</returns>
    public bool ChangeAssignmentRole(UserId userId, ActivityRole role)
    {
        var assignment =
            AssignmentOf(userId)
            ?? throw new InvalidOperationException("The person is not signed up.");
        if (assignment.Role == role)
        {
            return false;
        }

        assignments.Remove(assignment);
        assignments.Add(new Assignment(userId, Id, role, assignment.Status, assignment.CreatedAt));
        Raise(new AssignmentRoleChanged(Id, userId, role));
        return true;
    }

    /// <summary>
    /// Changes the status of the signup of a person.
    /// </summary>
    /// <param name="userId">Identifier of the signed-up person.</param>
    /// <param name="status">New status.</param>
    /// <returns>The status the signup had before.</returns>
    public AssignmentStatus ChangeAssignmentStatus(UserId userId, AssignmentStatus status)
    {
        var assignment =
            AssignmentOf(userId)
            ?? throw new InvalidOperationException("The person is not signed up.");
        var previous = assignment.Status;
        assignment.ChangeStatus(status);
        if (previous != status)
        {
            Raise(new AssignmentStatusChanged(Id, userId, previous, status, assignment.Role));
        }

        return previous;
    }

    /// <summary>
    /// Marks the activity as deleted, so its thumbnail can be released once it is gone.
    /// </summary>
    public void Delete()
    {
        Raise(new ActivityDeleted(Id, [ThumbnailId]));
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
        Modality = details.Modality;
        ThumbnailId = details.ThumbnailId;
        ActivityStartsAt = schedule.StartsAt;
        ActivityEndsAt = schedule.EndsAt;
        SyncRoleCapacities(capacities);
    }

    private void SyncRoleCapacities(RoleCapacityPlan plan)
    {
        var desiredByRole = plan.Items.ToDictionary(item => item.Role, item => item.DesiredCount);
        roleCapacities.RemoveAll(capacity => !desiredByRole.ContainsKey(capacity.Role));

        foreach (var (role, desiredCount) in desiredByRole)
        {
            var existing = roleCapacities.FirstOrDefault(capacity => capacity.Role == role);
            if (existing is null)
            {
                roleCapacities.Add(new ActivityRoleCapacity(Id, role, desiredCount));
            }
            else
            {
                existing.Resize(desiredCount);
            }
        }
    }
}
