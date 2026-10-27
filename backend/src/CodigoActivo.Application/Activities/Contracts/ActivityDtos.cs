namespace CodigoActivo.Application.Activities.Contracts;

/// <summary>
/// Contains the activity data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the activity.</param>
/// <param name="Title">Title of the activity.</param>
/// <param name="Description">Plain-text description.</param>
/// <param name="Location">Where it takes place.</param>
/// <param name="ActivityStartsAt">When it starts, in UTC.</param>
/// <param name="ActivityEndsAt">When it ends, in UTC.</param>
/// <param name="EventId">Identifier of the event the activity belongs to.</param>
/// <param name="ModalityId">Catalog identifier of how people attend it.</param>
/// <param name="ModalityName">Display name of the modality.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
/// <param name="RoleCapacities">
/// Desired number of people per role, ordered by role identifier.
/// </param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
public record ActivityResponse(
    Guid Id,
    string Title,
    string Description,
    string Location,
    DateTimeOffset ActivityStartsAt,
    DateTimeOffset ActivityEndsAt,
    Guid EventId,
    Guid ModalityId,
    string ModalityName,
    Guid ThumbnailId,
    IReadOnlyList<ActivityRoleCapacityResponse> RoleCapacities,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
)
{
    /// <summary>
    /// Initializes an empty activity response for serialization.
    /// </summary>
    public ActivityResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            default,
            default,
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            Guid.Empty,
            [],
            default,
            null
        ) { }
}

/// <summary>
/// Contains the activity role capacity data returned by the API.
/// </summary>
/// <param name="ActivityRoleTypeId">Catalog identifier of the role.</param>
/// <param name="DesiredCount">Number of people wanted in the role.</param>
/// <param name="IsHighDemand">
/// Whether more people than <paramref name="DesiredCount"/> are signed up in the role, denied
/// signups aside.
/// </param>
public record ActivityRoleCapacityResponse(
    Guid ActivityRoleTypeId,
    int DesiredCount,
    bool IsHighDemand
);

/// <summary>
/// Contains the assignment data returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the person signed up.</param>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="RoleTypeId">Catalog identifier of the role taken.</param>
/// <param name="RoleTypeName">Display name of the role.</param>
/// <param name="Status">Review status of the signup.</param>
public record AssignmentResponse(
    Guid UserId,
    Guid ActivityId,
    Guid RoleTypeId,
    string? RoleTypeName,
    AssignmentStatusResponse Status
);

/// <summary>
/// Contains the assignment status data returned by the API.
/// </summary>
/// <param name="Id">Catalog identifier of the status.</param>
/// <param name="Name">Display name of the status.</param>
public record AssignmentStatusResponse(Guid Id, string Name);

/// <summary>
/// Contains the signup of a household member to an activity, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the household member.</param>
/// <param name="FirstName">Member's given name.</param>
/// <param name="LastName">Member's family name.</param>
/// <param name="RoleTypeId">Catalog identifier of the role taken.</param>
/// <param name="RoleName">Display name of the role.</param>
/// <param name="StatusId">Catalog identifier of the review status.</param>
/// <param name="StatusName">Display name of the status.</param>
public record HouseholdMemberAssignmentResponse(
    Guid ActivityId,
    Guid UserId,
    string FirstName,
    string LastName,
    Guid RoleTypeId,
    string RoleName,
    Guid StatusId,
    string StatusName
);

/// <summary>
/// Contains a role a person may sign up in, returned by the API.
/// </summary>
/// <param name="Id">Catalog identifier of the role.</param>
/// <param name="Name">Display name of the role.</param>
public record SignupRoleResponse(Guid Id, string Name);

/// <summary>
/// Contains the roles a household member may sign up in, returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the household member.</param>
/// <param name="Roles">Roles the type of the member allows.</param>
public record HouseholdSignupRolesResponse(Guid UserId, IReadOnlyList<SignupRoleResponse> Roles);

/// <summary>
/// Contains an activity whose time range overlaps another, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the overlapping activity.</param>
/// <param name="Title">Title of the overlapping activity.</param>
/// <param name="StartsAt">When it starts, in UTC.</param>
/// <param name="EndsAt">When it ends, in UTC.</param>
public record OverlappingActivityResponse(
    Guid ActivityId,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt
);

/// <summary>
/// Contains the signups of a person that overlap in time with an activity, returned by the API.
/// </summary>
/// <param name="HasOverlaps">Whether <paramref name="Overlaps"/> lists any activity.</param>
/// <param name="Overlaps">Overlapping activities, ordered by start.</param>
public record TimeOverlapResponse(
    bool HasOverlaps,
    IReadOnlyList<OverlappingActivityResponse> Overlaps
);

/// <summary>
/// Contains an activity the signed-in user is signed up to, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">Title of the activity.</param>
/// <param name="Description">Plain-text description of the activity.</param>
/// <param name="ActivityStartsAt">When it starts, in UTC.</param>
/// <param name="ActivityEndsAt">When it ends, in UTC.</param>
/// <param name="EventId">Identifier of the event the activity belongs to.</param>
/// <param name="RoleType">Role the user signed up in.</param>
/// <param name="Status">Review status of the signup.</param>
public record AssignedActivityResponse(
    Guid ActivityId,
    string Title,
    string Description,
    DateTimeOffset ActivityStartsAt,
    DateTimeOffset ActivityEndsAt,
    Guid EventId,
    AssignedActivityRoleResponse RoleType,
    AssignedActivityStatusResponse Status
)
{
    /// <summary>
    /// Initializes an empty assigned activity response for serialization.
    /// </summary>
    public AssignedActivityResponse()
        : this(Guid.Empty, string.Empty, string.Empty, default, default, Guid.Empty, null!, null!)
    { }
}

/// <summary>
/// Contains the role of a signup, returned by the API.
/// </summary>
/// <param name="Id">Catalog identifier of the role.</param>
/// <param name="Name">Display name of the role.</param>
public record AssignedActivityRoleResponse(Guid Id, string Name);

/// <summary>
/// Contains the review status of a signup, returned by the API.
/// </summary>
/// <param name="Id">Catalog identifier of the status.</param>
/// <param name="Name">Display name of the status.</param>
public record AssignedActivityStatusResponse(Guid Id, string Name);

/// <summary>
/// Contains the activity role type data returned by the API.
/// </summary>
/// <param name="Id">Catalog identifier of the role.</param>
/// <param name="Name">Display name of the role.</param>
/// <param name="Description">Description of the role shown to people.</param>
public record ActivityRoleTypeResponse(Guid Id, string Name, string Description)
{
    /// <summary>
    /// Initializes an empty activity role type response for serialization.
    /// </summary>
    public ActivityRoleTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains the assignment status type data returned by the API.
/// </summary>
/// <param name="Id">Catalog identifier of the status.</param>
/// <param name="Name">Display name of the status.</param>
/// <param name="Description">Description of the status shown to people.</param>
/// <param name="Color">Color the status is shown with.</param>
public record AssignmentStatusTypeResponse(Guid Id, string Name, string Description, string Color)
{
    /// <summary>
    /// Initializes an empty assignment status type response for serialization.
    /// </summary>
    public AssignmentStatusTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains the activity modality type data returned by the API.
/// </summary>
/// <param name="Id">Catalog identifier of the modality.</param>
/// <param name="Name">Display name of the modality.</param>
public record ActivityModalityTypeResponse(Guid Id, string Name)
{
    /// <summary>
    /// Initializes an empty activity modality type response for serialization.
    /// </summary>
    public ActivityModalityTypeResponse()
        : this(Guid.Empty, string.Empty) { }
}
