using System.ComponentModel.DataAnnotations;

namespace CodigoActivo.Application.DTOs;

public record ActivityAllowedRoleResponse(
    Guid RoleTypeId,
    string? RoleTypeName,
    int? DesiredSignups
);

public record ActivityResponse(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset? ActivityStartsAt,
    DateTimeOffset? ActivityEndsAt,
    Guid EventId,
    Guid ThumbnailId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    IReadOnlyList<ActivityAllowedRoleResponse> AllowedRoleTypes
);

public record ActivityAllowedRoleRequest(Guid ActivityRoleTypeId, int? DesiredSignups);

public record CreateActivityRequest(
    [Required, MaxLength(200)] string Title,
    [Required] string Description,
    DateTimeOffset? ActivityStartsAt,
    DateTimeOffset? ActivityEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<ActivityAllowedRoleRequest>? AllowedRoleTypes
);

public record UpdateActivityRequest(
    [Required, MaxLength(200)] string Title,
    [Required] string Description,
    DateTimeOffset? ActivityStartsAt,
    DateTimeOffset? ActivityEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<ActivityAllowedRoleRequest>? AllowedRoleTypes
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

public record ChangeAssignmentStatusRequest([Required] Guid AssignmentStatusId);

public record OverlappingActivityResponse(
    Guid ActivityId,
    string Title,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt
);

public record TimeOverlapResponse(
    bool HasOverlaps,
    IReadOnlyList<OverlappingActivityResponse> Overlaps
);

public record AssignedActivityResponse(
    Guid ActivityId,
    string Title,
    string Description,
    DateTimeOffset? ActivityStartsAt,
    DateTimeOffset? ActivityEndsAt,
    Guid EventId,
    AssignedActivityRoleResponse RoleType,
    AssignedActivityStatusResponse Status
);

public record AssignedActivityRoleResponse(Guid Id, string Name);

public record AssignedActivityStatusResponse(Guid Id, string Name);

public record ActivityRoleTypeResponse(Guid Id, string Name, string Description);

public record CreateActivityRoleTypeRequest(string Name, string Description);

public record UpdateActivityRoleTypeRequest(string Name, string Description);

public record AssignmentStatusTypeResponse(Guid Id, string Name, string Description);
