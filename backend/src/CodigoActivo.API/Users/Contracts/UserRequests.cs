using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CodigoActivo.Application.Common.Mapping;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Users;
using Gender = CodigoActivo.Application.Abstractions.Querying.ReadModel.Gender;

namespace CodigoActivo.API.Users.Contracts;

/// <summary>
/// Contains the client-supplied data used to grant or revoke the administrator rights of a user.
/// </summary>
/// <param name="IsAdmin">
/// Whether the user must be an administrator. It must be present: a body without it is refused
/// instead of being read as a revocation.
/// </param>
/// <param name="CurrentPassword">
/// Password of the acting administrator. Required to grant the role; ignored when revoking it.
/// </param>
public record SetAdminRequest(
    [property: JsonRequired] bool IsAdmin,
    [MaxLength(128)] string? CurrentPassword
)
{
    /// <summary>
    /// Builds the command that grants or revokes the rights.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <returns>The command.</returns>
    public SetAdminCommand ToCommand(UserId userId)
    {
        return new SetAdminCommand(userId, IsAdmin, CurrentPassword);
    }
}

/// <summary>
/// Contains the client-supplied data used by an administrator to reset a user's second factor.
/// </summary>
/// <param name="CurrentPassword">Password of the acting administrator, re-entered to authorize the reset.</param>
public record ResetTwoFactorRequest([Required] [MaxLength(128)] string CurrentPassword)
{
    /// <summary>
    /// Builds the command that resets the second factor.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <returns>The command.</returns>
    public ResetTwoFactorCommand ToCommand(UserId userId)
    {
        return new ResetTwoFactorCommand(userId, CurrentPassword);
    }
}

/// <summary>
/// Contains the client-supplied data that replaces the personal data of a user.
/// </summary>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">
/// Email; required for an independent account and ignored for a dependent. A new one only applies
/// once its holder confirms it through the emailed link.
/// </param>
/// <param name="Phone">Phone; required for an independent account and ignored for a dependent.</param>
/// <param name="BirthDate">
/// User's date of birth. Required for a dependent; a changed value must keep them a minor,
/// while the stored one stays valid after they come of age. An independent account must leave it
/// unset.
/// </param>
/// <param name="NationalId">
/// DNI or NIE. Required for an independent account, never checked for uniqueness; ignored for a
/// dependent.
/// </param>
/// <param name="NewsConsent">
/// Whether the user agrees to receive news by email; ignored for a dependent.
/// </param>
/// <param name="Gender">Gender of the user.</param>
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
    [Required] [MaxLength(120)] string FirstName,
    [Required] [MaxLength(120)] string LastName,
    [EmailAddress] [MaxLength(256)] string? Email,
    [MaxLength(40)] string? Phone,
    DateOnly? BirthDate,
    [MaxLength(12)] string? NationalId,
    bool NewsConsent,
    [EnumDataType(typeof(Gender))] Gender Gender,
    Guid? ParentId,
    [MaxLength(128)] string? CurrentPassword,
    [MaxLength(40)] string? SecondaryPhone = null
)
{
    /// <summary>
    /// Builds the command that updates the user.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <returns>The command.</returns>
    public UpdateUserCommand ToCommand(UserId userId)
    {
        return new UpdateUserCommand(
            userId,
            FirstName,
            LastName,
            Email,
            Phone,
            BirthDate,
            NationalId,
            NewsConsent,
            Gender.ToDomain(),
            ParentId is { } parentId ? UserId.From(parentId) : null,
            CurrentPassword,
            SecondaryPhone
        );
    }
}
