using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

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

public record ActivityRoleCapacityResponse(
    Guid ActivityRoleTypeId,
    int DesiredCount,
    bool IsHighDemand
);

public record ActivityRoleCapacityRequest(
    [Required] Guid ActivityRoleTypeId,
    [Required] [Range(1, 10000)] int? DesiredCount
);

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

public record AssignmentResponse(
    Guid UserId,
    Guid ActivityId,
    Guid RoleTypeId,
    string? RoleTypeName,
    AssignmentStatusResponse Status
);

public record AssignmentStatusResponse(Guid Id, string Name);

public record AssignRequest([Required] Guid ActivityRoleTypeId);

public record AssignHouseholdRequest(
    [Required] IReadOnlyList<HouseholdAssignmentRequest> Assignments
);

public record HouseholdAssignmentRequest(
    [Required] Guid UserId,
    [Required] Guid ActivityRoleTypeId
);

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

public record SignupRoleResponse(Guid Id, string Name);

public record HouseholdSignupRolesResponse(Guid UserId, IReadOnlyList<SignupRoleResponse> Roles);

public record ChangeAssignmentStatusRequest([Required] Guid AssignmentStatusId);

public record ChangeAssignmentRoleRequest([Required] Guid ActivityRoleTypeId);

public record OverlappingActivityResponse(
    Guid ActivityId,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt
);

public record TimeOverlapResponse(
    bool HasOverlaps,
    IReadOnlyList<OverlappingActivityResponse> Overlaps
);

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
    public AssignedActivityResponse()
        : this(Guid.Empty, string.Empty, string.Empty, default, default, Guid.Empty, null!, null!)
    { }
}

public record AssignedActivityRoleResponse(Guid Id, string Name);

public record AssignedActivityStatusResponse(Guid Id, string Name);

public record ActivityRoleTypeResponse(Guid Id, string Name, string Description)
{
    public ActivityRoleTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty) { }
}

public record AssignmentStatusTypeResponse(Guid Id, string Name, string Description, string Color)
{
    public AssignmentStatusTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}

public record ActivityModalityTypeResponse(Guid Id, string Name)
{
    public ActivityModalityTypeResponse()
        : this(Guid.Empty, string.Empty) { }
}
