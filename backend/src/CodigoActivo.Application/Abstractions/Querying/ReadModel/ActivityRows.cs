namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Stored activity as the queries read it.
/// </summary>
public sealed class ActivityRow
{
    /// <summary>Gets the identifier of the activity.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets where the activity takes place.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>Gets when the activity starts.</summary>
    public DateTimeOffset ActivityStartsAt { get; init; }

    /// <summary>Gets when the activity ends.</summary>
    public DateTimeOffset ActivityEndsAt { get; init; }

    /// <summary>Gets the identifier of the event.</summary>
    public Guid EventId { get; init; }

    /// <summary>Gets the event.</summary>
    public EventRow Event { get; init; } = null!;

    /// <summary>Gets the identifier of the modality.</summary>
    public Guid ActivityModalityTypeId { get; init; }

    /// <summary>Gets the modality.</summary>
    public ActivityModalityTypeRow ActivityModalityType { get; init; } = null!;

    /// <summary>Gets the identifier of the thumbnail file.</summary>
    public Guid ThumbnailId { get; init; }

    /// <summary>Gets when the activity was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the activity was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Gets the identifier of the author.</summary>
    public Guid CreatedBy { get; init; }

    /// <summary>Gets the identifier of the last editor.</summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>Gets the assignments of the activity.</summary>
    public ICollection<AssignmentRow> Assignments { get; init; } = [];

    /// <summary>Gets the desired people per role.</summary>
    public ICollection<RoleCapacityRow> RoleCapacities { get; init; } = [];
}

/// <summary>
/// Stored assignment of a user to an activity as the queries read it.
/// </summary>
public sealed class AssignmentRow
{
    /// <summary>Gets the identifier of the user.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the user.</summary>
    public UserRow User { get; init; } = null!;

    /// <summary>Gets the identifier of the activity.</summary>
    public Guid ActivityId { get; init; }

    /// <summary>Gets the activity.</summary>
    public ActivityRow Activity { get; init; } = null!;

    /// <summary>Gets the identifier of the role.</summary>
    public Guid ActivityRoleTypeId { get; init; }

    /// <summary>Gets the role.</summary>
    public ActivityRoleTypeRow ActivityRoleType { get; init; } = null!;

    /// <summary>Gets the identifier of the status.</summary>
    public Guid AssignmentStatusId { get; init; }

    /// <summary>Gets the status.</summary>
    public AssignmentStatusTypeRow AssignmentStatus { get; init; } = null!;

    /// <summary>Gets when the signup was requested.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Stored desired number of people for a role of an activity as the queries read it.
/// </summary>
public sealed class RoleCapacityRow
{
    /// <summary>Gets the identifier of the activity.</summary>
    public Guid ActivityId { get; init; }

    /// <summary>Gets the activity.</summary>
    public ActivityRow Activity { get; init; } = null!;

    /// <summary>Gets the identifier of the role.</summary>
    public Guid ActivityRoleTypeId { get; init; }

    /// <summary>Gets the role.</summary>
    public ActivityRoleTypeRow ActivityRoleType { get; init; } = null!;

    /// <summary>Gets the desired number of people.</summary>
    public int DesiredCount { get; init; }
}

/// <summary>
/// Stored activity role as the queries read it.
/// </summary>
public sealed class ActivityRoleTypeRow
{
    /// <summary>Gets the identifier of the role.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the assignments made with the role.</summary>
    public ICollection<AssignmentRow> Assignments { get; init; } = [];
}

/// <summary>
/// Stored assignment status as the queries read it.
/// </summary>
public sealed class AssignmentStatusTypeRow
{
    /// <summary>Gets the identifier of the status.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the display color.</summary>
    public string Color { get; init; } = string.Empty;
}

/// <summary>
/// Stored activity modality as the queries read it.
/// </summary>
public sealed class ActivityModalityTypeRow
{
    /// <summary>Gets the identifier of the modality.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;
}
