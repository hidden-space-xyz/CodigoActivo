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
/// <param name="SecondaryPhone">Optional second contact phone; dependents never have one.</param>
/// <param name="BirthDate">User's date of birth; only dependents have one.</param>
/// <param name="NationalId">Normalized DNI or NIE; only independent accounts have one.</param>
/// <param name="PromotionalConsent">Whether the user agreed to receive promotional content.</param>
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
/// <param name="TwoFactorMethod">Second factor the user presents when logging in.</param>
public record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? SecondaryPhone,
    DateOnly? BirthDate,
    string? NationalId,
    bool PromotionalConsent,
    Gender Gender,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ParentId,
    string? ParentName,
    int? DependentCount,
    UserStatusResponse Status,
    bool IsAdmin,
    UserTypeSummaryResponse? Type,
    TwoFactorMethod TwoFactorMethod
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
            null,
            null,
            null,
            false,
            default,
            null,
            default,
            null,
            null,
            null,
            null,
            null!,
            false,
            null,
            TwoFactorMethod.Email
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
/// <param name="CurrentPassword">
/// Password of the acting administrator. Required to grant the role; ignored when revoking it.
/// </param>
public record SetAdminRequest(bool IsAdmin, [MaxLength(128)] string? CurrentPassword);

/// <summary>
/// Contains the client-supplied data used by an administrator to reset a user's second factor.
/// </summary>
/// <param name="CurrentPassword">Password of the acting administrator, re-entered to authorize the reset.</param>
public record ResetTwoFactorRequest([Required] [MaxLength(128)] [NotBlank] string CurrentPassword);

/// <summary>
/// Contains the client-supplied data used to request the code that confirms account deletion.
/// </summary>
/// <param name="CurrentPassword">Password of the signed-in user, re-entered to authorize the request.</param>
public record AccountDeletionCodeRequest(
    [Required] [MaxLength(128)] [NotBlank] string CurrentPassword
);

/// <summary>
/// Contains the client-supplied data used to delete the signed-in user's own account.
/// </summary>
/// <param name="CurrentPassword">Password of the signed-in user, re-entered to authorize the deletion.</param>
/// <param name="Code">Code from the email or the authenticator application.</param>
public record DeleteAccountRequest(
    [Required] [MaxLength(128)] [NotBlank] string CurrentPassword,
    [Required] [MaxLength(16)] [NotBlank] string Code
);

/// <summary>
/// Tells the signed-in user whether they may delete their own account.
/// </summary>
/// <param name="Allowed">
/// Whether the deletion can be requested; <see langword="false"/> only for the last administrator.
/// </param>
public record AccountDeletionStatusResponse(bool Allowed);

/// <summary>
/// Contains the client-supplied data used to update the user.
/// </summary>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
/// <param name="BirthDate">
/// User's date of birth. Required for a dependent; a changed value must keep them a minor,
/// while the stored one stays valid after they come of age. An independent account must leave it
/// unset.
/// </param>
/// <param name="NationalId">
/// DNI or NIE. Required for an independent account, never checked for uniqueness; ignored for a
/// dependent.
/// </param>
/// <param name="PromotionalConsent">
/// Whether the user agrees to receive promotional content; ignored for a dependent.
/// </param>
/// <param name="Gender">The gender value.</param>
/// <param name="ParentId">
/// Identifier of the parent. Only accepted for an account that is already a dependent, and only
/// repeating the guardian it already has; the guardian is never reassigned here.
/// </param>
/// <param name="CurrentPassword">
/// Password of the acting caller. Required when the update changes the email, the phone or the
/// secondary phone of the target account; ignored otherwise.
/// </param>
/// <param name="SecondaryPhone">
/// Optional second contact phone; blank removes it and it must differ from
/// <paramref name="Phone"/>. Ignored for a dependent.
/// </param>
public record UpdateUserRequest(
    [Required] [MaxLength(120)] [NotBlank] string FirstName,
    [Required] [MaxLength(120)] [NotBlank] string LastName,
    [EmailAddress] [MaxLength(256)] string? Email,
    [Phone] [MaxLength(40)] string? Phone,
    [NotDefaultOrFutureDate] DateOnly? BirthDate,
    [MaxLength(12)] [SpanishNationalId] string? NationalId,
    bool PromotionalConsent,
    [EnumDataType(typeof(Gender))] Gender Gender,
    Guid? ParentId,
    [MaxLength(128)] string? CurrentPassword,
    [Phone] [MaxLength(40)] string? SecondaryPhone = null
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
