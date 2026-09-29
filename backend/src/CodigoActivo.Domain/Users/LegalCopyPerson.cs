namespace CodigoActivo.Domain.Users;

/// <summary>
/// An erased account, or one of its dependents, as the legal copy keeps it. Catalog names are
/// copied by value, since the rows they come from may change later.
/// </summary>
/// <param name="Id">Identifier of the account.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Email address.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="SecondaryPhone">Secondary phone number.</param>
/// <param name="NationalId">DNI or NIE.</param>
/// <param name="BirthDate">Birth date.</param>
/// <param name="Gender">Gender.</param>
/// <param name="PromotionalConsent">Whether promotional content was accepted.</param>
/// <param name="UserTypeId">Identifier of the user type.</param>
/// <param name="UserTypeName">Name of the user type.</param>
/// <param name="UserStatusTypeId">Identifier of the account status.</param>
/// <param name="UserStatusTypeName">Name of the account status.</param>
/// <param name="IsAdmin">Whether the user was an administrator.</param>
/// <param name="TwoFactorMethod">Second factor in use.</param>
/// <param name="ParentId">Identifier of the guardian, for dependents.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="UpdatedAt">When the account was last updated.</param>
/// <param name="LastLoginAt">When the user last logged in.</param>
public sealed record LegalCopyPerson(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? SecondaryPhone,
    string? NationalId,
    DateOnly? BirthDate,
    Gender Gender,
    bool PromotionalConsent,
    Guid UserTypeId,
    string UserTypeName,
    Guid UserStatusTypeId,
    string UserStatusTypeName,
    bool IsAdmin,
    TwoFactorMethod TwoFactorMethod,
    Guid? ParentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? LastLoginAt
)
{
    /// <summary>
    /// Gets the members that hold catalog names instead of a <see cref="User"/> property.
    /// </summary>
    public static IReadOnlyList<string> CatalogNameProperties { get; } =
    [nameof(UserTypeName), nameof(UserStatusTypeName)];
}
