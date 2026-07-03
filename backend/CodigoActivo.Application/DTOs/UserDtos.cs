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
)
{
    public UserResponse()
        : this(Guid.Empty, "", "", null, null, default, null, default, null, null, null!, []) { }
}

public record UserStatusResponse(Guid Id, string Name, string Color);

public record UserRoleResponse(Guid Id, string Name, string Color);

public record UpdateUserRequest(
    [Required, MaxLength(120)] string FirstName,
    [Required, MaxLength(120)] string LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    [Phone, MaxLength(40)] string? Phone,
    DateOnly BirthDate,
    Guid? ParentId
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(128)] string NewPassword
);

public record UserStatusTypeResponse(Guid Id, string Name, string Description, string Color)
{
    public UserStatusTypeResponse()
        : this(Guid.Empty, "", "", "") { }
}

public record UserTypeResponse(Guid Id, string Name, string Description, string Color)
{
    public UserTypeResponse()
        : this(Guid.Empty, "", "", "") { }
}

public record RegistrationTypeResponse(
    Guid Id,
    string Name,
    string Description,
    string Color,
    bool IsAllowedForMinors,
    bool IsAllowedForAdults
)
{
    public RegistrationTypeResponse()
        : this(Guid.Empty, "", "", "", false, false) { }
}
