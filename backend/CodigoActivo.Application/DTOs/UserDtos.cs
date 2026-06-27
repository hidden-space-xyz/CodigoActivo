using System.ComponentModel.DataAnnotations;

namespace CodigoActivo.Application.DTOs;

public record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateOnly BirthDate,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ParentId,
    UserStatusResponse Status,
    IReadOnlyList<UserRoleResponse> Roles
);

public record UserStatusResponse(Guid Id, string Name);

public record UserRoleResponse(Guid Id, string Name);

public record UpdateUserRequest(
    [Required, MaxLength(120)] string FirstName,
    [Required, MaxLength(120)] string LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    [Phone, MaxLength(40)] string? Phone,
    DateOnly BirthDate,
    Guid? ParentId
);

public record SetUserRoleRequest(Guid RoleId);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(128)] string NewPassword
);

public record UserStatusTypeResponse(Guid Id, string Name, string Description);

public record UserTypeResponse(Guid Id, string Name, string Description);
