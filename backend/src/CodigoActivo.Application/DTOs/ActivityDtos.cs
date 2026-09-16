using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the activity data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Location">The location value.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="ModalityId">Identifier of the modality.</param>
/// <param name="ModalityName">The modality name value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="RoleCapacities">The role capacities value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="CreatedBy">The created by value.</param>
/// <param name="UpdatedBy">The updated by value.</param>
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
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy
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
            null,
            Guid.Empty,
            null
        ) { }
}

/// <summary>
/// Contains the activity role capacity data returned by the API.
/// </summary>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
/// <param name="DesiredCount">Number of desired allowed or reported.</param>
/// <param name="IsHighDemand">Whether high demand.</param>
public record ActivityRoleCapacityResponse(
    Guid ActivityRoleTypeId,
    int DesiredCount,
    bool IsHighDemand
);

/// <summary>
/// Contains the client-supplied data used to activity role capacity.
/// </summary>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
/// <param name="DesiredCount">Number of desired allowed or reported.</param>
public record ActivityRoleCapacityRequest(
    [Required] Guid ActivityRoleTypeId,
    [Required] [Range(1, 10000)] int? DesiredCount
);

/// <summary>
/// Contains the client-supplied data used to create an activity.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Location">The location value.</param>
/// <param name="ActivityModalityTypeId">Identifier of the activity modality type.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="RoleCapacities">The role capacities value.</param>
public record CreateActivityRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(4000)] [NotBlank] string Description,
    [Required] [MaxLength(200)] [NotBlank] string Location,
    [Required] Guid ActivityModalityTypeId,
    [Required] DateTimeOffset? ActivityStartsAt,
    [Required] DateTimeOffset? ActivityEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<ActivityRoleCapacityRequest>? RoleCapacities
);

/// <summary>
/// Contains the client-supplied data used to update the activity.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Location">The location value.</param>
/// <param name="ActivityModalityTypeId">Identifier of the activity modality type.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="RoleCapacities">The role capacities value.</param>
public record UpdateActivityRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(4000)] [NotBlank] string Description,
    [Required] [MaxLength(200)] [NotBlank] string Location,
    [Required] Guid ActivityModalityTypeId,
    [Required] DateTimeOffset? ActivityStartsAt,
    [Required] DateTimeOffset? ActivityEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<ActivityRoleCapacityRequest>? RoleCapacities
);

/// <summary>
/// Contains the assignment data returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="RoleTypeId">Identifier of the role type.</param>
/// <param name="RoleTypeName">The role type name value.</param>
/// <param name="Status">The status value.</param>
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
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
public record AssignmentStatusResponse(Guid Id, string Name);

/// <summary>
/// Contains the client-supplied data used to assign.
/// </summary>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
/// <param name="AcceptTerms">Whether accept terms.</param>
public record AssignRequest([Required] Guid ActivityRoleTypeId, bool AcceptTerms = false);

/// <summary>
/// Contains the client-supplied data used to assign household.
/// </summary>
/// <param name="Assignments">The assignments value.</param>
/// <param name="AcceptTerms">Whether accept terms.</param>
public record AssignHouseholdRequest(
    [Required] IReadOnlyList<HouseholdAssignmentRequest> Assignments,
    bool AcceptTerms = false
);

/// <summary>
/// Contains the client-supplied data used to household assignment.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
public record HouseholdAssignmentRequest(
    [Required] Guid UserId,
    [Required] Guid ActivityRoleTypeId
);

/// <summary>
/// Contains the household member assignment data returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="RoleTypeId">Identifier of the role type.</param>
/// <param name="RoleName">The role name value.</param>
/// <param name="StatusId">Identifier of the status.</param>
/// <param name="StatusName">The status name value.</param>
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
/// Contains the signup role data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
public record SignupRoleResponse(Guid Id, string Name);

/// <summary>
/// Contains the household signup roles data returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Roles">The roles value.</param>
public record HouseholdSignupRolesResponse(Guid UserId, IReadOnlyList<SignupRoleResponse> Roles);

/// <summary>
/// Contains the client-supplied data used to change assignment status.
/// </summary>
/// <param name="AssignmentStatusId">Identifier of the assignment status.</param>
public record ChangeAssignmentStatusRequest([Required] Guid AssignmentStatusId);

/// <summary>
/// Contains the client-supplied data used to change assignment role.
/// </summary>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
public record ChangeAssignmentRoleRequest([Required] Guid ActivityRoleTypeId);

/// <summary>
/// Contains the overlapping activity data returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">The title value.</param>
/// <param name="StartsAt">The starts at value.</param>
/// <param name="EndsAt">The ends at value.</param>
public record OverlappingActivityResponse(
    Guid ActivityId,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt
);

/// <summary>
/// Contains the time overlap data returned by the API.
/// </summary>
/// <param name="HasOverlaps">Whether has overlaps.</param>
/// <param name="Overlaps">The overlaps value.</param>
public record TimeOverlapResponse(
    bool HasOverlaps,
    IReadOnlyList<OverlappingActivityResponse> Overlaps
);

/// <summary>
/// Contains the assigned activity data returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Description">The description value.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="RoleType">The role type value.</param>
/// <param name="Status">The status value.</param>
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
/// Contains the assigned activity role data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
public record AssignedActivityRoleResponse(Guid Id, string Name);

/// <summary>
/// Contains the assigned activity status data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
public record AssignedActivityStatusResponse(Guid Id, string Name);

/// <summary>
/// Contains the activity role type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
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
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Color">The color value.</param>
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
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
public record ActivityModalityTypeResponse(Guid Id, string Name)
{
    /// <summary>
    /// Initializes an empty activity modality type response for serialization.
    /// </summary>
    public ActivityModalityTypeResponse()
        : this(Guid.Empty, string.Empty) { }
}
