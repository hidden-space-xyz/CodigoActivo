using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

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
    bool IsAdmin,
    UserTypeSummaryResponse? Type
)
{
    public UserResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            default,
            null,
            default,
            null,
            null,
            null!,
            false,
            null
        ) { }
}

public record UserStatusResponse(Guid Id, string Name, string Color);

public record UserTypeSummaryResponse(Guid Id, string Name, string Color);

public record SetAdminRequest(bool IsAdmin);

public record UpdateUserRequest(
    [Required] [MaxLength(120)] [NotBlank] string FirstName,
    [Required] [MaxLength(120)] [NotBlank] string LastName,
    [EmailAddress] [MaxLength(256)] string? Email,
    [Phone] [MaxLength(40)] string? Phone,
    [NotDefaultOrFutureDate] DateOnly BirthDate,
    Guid? ParentId
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required] [MinLength(8)] [MaxLength(128)] string NewPassword
);

public record UserStatusTypeResponse(Guid Id, string Name, string Description, string Color)
{
    public UserStatusTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}

public record UserTypeResponse(Guid Id, string Name, string Description, string Color)
{
    public UserTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}
