using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the user data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
/// <param name="BirthDate">User's date of birth.</param>
/// <param name="Gender">The gender value.</param>
/// <param name="LastLoginAt">UTC timestamp of the user's most recent login.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ParentId">Identifier of the parent.</param>
/// <param name="ParentName">The parent name value.</param>
/// <param name="DependentCount">Number of dependents linked to the user.</param>
/// <param name="Status">The status value.</param>
/// <param name="IsAdmin">Whether admin.</param>
/// <param name="Type">The type value.</param>
public record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateOnly BirthDate,
    Gender Gender,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ParentId,
    string? ParentName,
    int? DependentCount,
    UserStatusResponse Status,
    bool IsAdmin,
    UserTypeSummaryResponse? Type
)
{
    /// <summary>
    /// Initializes an empty user response for serialization.
    /// </summary>
    public UserResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            default,
            default,
            null,
            default,
            null,
            null,
            null,
            null,
            null!,
            false,
            null
        ) { }
}

/// <summary>
/// Contains the user status data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record UserStatusResponse(Guid Id, string Name, string Color);

/// <summary>
/// Contains the user type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record UserTypeSummaryResponse(Guid Id, string Name, string Color);

/// <summary>
/// Contains the client-supplied data used to set admin.
/// </summary>
/// <param name="IsAdmin">Whether admin.</param>
public record SetAdminRequest(bool IsAdmin);

/// <summary>
/// Contains the client-supplied data used to update the user.
/// </summary>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
/// <param name="BirthDate">User's date of birth.</param>
/// <param name="Gender">The gender value.</param>
/// <param name="ParentId">Identifier of the parent.</param>
public record UpdateUserRequest(
    [Required] [MaxLength(120)] [NotBlank] string FirstName,
    [Required] [MaxLength(120)] [NotBlank] string LastName,
    [EmailAddress] [MaxLength(256)] string? Email,
    [Phone] [MaxLength(40)] string? Phone,
    [NotDefaultOrFutureDate] DateOnly BirthDate,
    [EnumDataType(typeof(Gender))] Gender Gender,
    Guid? ParentId
);

/// <summary>
/// Contains the client-supplied data used to change password.
/// </summary>
/// <param name="CurrentPassword">The current password value.</param>
/// <param name="NewPassword">The new password value.</param>
public record ChangePasswordRequest(
    [Required] [MaxLength(128)] [NotBlank] string CurrentPassword,
    [Required] [MinLength(12)] [MaxLength(128)] [NotBlank] string NewPassword
);

/// <summary>
/// Contains the user status type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Color">The color value.</param>
public record UserStatusTypeResponse(Guid Id, string Name, string Description, string Color)
{
    /// <summary>
    /// Initializes an empty user status type response for serialization.
    /// </summary>
    public UserStatusTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains the user type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Color">The color value.</param>
public record UserTypeResponse(Guid Id, string Name, string Description, string Color)
{
    /// <summary>
    /// Initializes an empty user type response for serialization.
    /// </summary>
    public UserTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}
