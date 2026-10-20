namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Stored account as the queries read it.
/// </summary>
public sealed class UserRow
{
    /// <summary>Gets the identifier of the account.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the given name.</summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Gets the family name.</summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>Gets the email address, absent for dependents.</summary>
    public string? Email { get; init; }

    /// <summary>Gets the new email address its holder asked for and has not confirmed yet.</summary>
    public string? PendingEmail { get; init; }

    /// <summary>Gets the main phone number.</summary>
    public string? Phone { get; init; }

    /// <summary>Gets the optional second phone number.</summary>
    public string? SecondaryPhone { get; init; }

    /// <summary>Gets the stored password hash, used only to derive credential stamps.</summary>
    public string? PasswordHash { get; init; }

    /// <summary>Gets the date of birth, kept for dependents.</summary>
    public DateOnly? BirthDate { get; init; }

    /// <summary>Gets the DNI or NIE of an independent account.</summary>
    public string? NationalId { get; init; }

    /// <summary>Gets a value indicating whether news email is accepted.</summary>
    public bool NewsConsent { get; init; }

    /// <summary>Gets the gender.</summary>
    public Gender Gender { get; init; }

    /// <summary>Gets the last successful login.</summary>
    public DateTimeOffset? LastLoginAt { get; init; }

    /// <summary>Gets when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the account was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Gets when the holder declared being of age while registering.</summary>
    public DateTimeOffset? AdultDeclaredAt { get; init; }

    /// <summary>
    /// Gets when the guardian declared being the dependent's parent or legal guardian.
    /// </summary>
    public DateTimeOffset? GuardianshipDeclaredAt { get; init; }

    /// <summary>
    /// Gets when the guardian was told that the dependent will be erased the day it comes of age.
    /// </summary>
    public DateTimeOffset? ComingOfAgeNoticeSentAt { get; init; }

    /// <summary>Gets the identifier of the guardian of a dependent.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Gets the guardian of a dependent.</summary>
    public UserRow? Parent { get; init; }

    /// <summary>Gets the identifier of the account status.</summary>
    public Guid UserStatusTypeId { get; init; }

    /// <summary>Gets the account status.</summary>
    public UserStatusTypeRow UserStatusType { get; init; } = null!;

    /// <summary>Gets the identifier of the membership type.</summary>
    public Guid UserTypeId { get; init; }

    /// <summary>Gets the membership type.</summary>
    public UserTypeRow UserType { get; init; } = null!;

    /// <summary>Gets a value indicating whether the account is an administrator.</summary>
    public bool IsAdmin { get; init; }

    /// <summary>Gets the second-factor method.</summary>
    public TwoFactorMethod TwoFactorMethod { get; init; } = TwoFactorMethod.Email;

    /// <summary>Gets the identifier of the pending second-factor challenge.</summary>
    public Guid? LoginChallengeId { get; init; }

    /// <summary>Gets when the pending second-factor challenge was opened.</summary>
    public DateTimeOffset? LoginChallengeStartedAt { get; init; }

    /// <summary>Gets when the account was locked after wrong passwords.</summary>
    public DateTimeOffset? PasswordLockedAt { get; init; }

    /// <summary>Gets the dependents of a guardian.</summary>
    public ICollection<UserRow> Children { get; init; } = [];

    /// <summary>Gets the activity assignments of the account.</summary>
    public ICollection<AssignmentRow> Assignments { get; init; } = [];
}

/// <summary>
/// Stored account status as the queries read it.
/// </summary>
public sealed class UserStatusTypeRow
{
    /// <summary>Gets the identifier of the status.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the display color.</summary>
    public string Color { get; init; } = string.Empty;
}

/// <summary>
/// Stored membership type as the queries read it.
/// </summary>
public sealed class UserTypeRow
{
    /// <summary>Gets the identifier of the type.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the display color.</summary>
    public string Color { get; init; } = string.Empty;
}

/// <summary>
/// Stored session as the queries read it.
/// </summary>
public sealed class UserSessionRow
{
    /// <summary>Gets the identifier of the session.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the account.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets when the session was opened.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the session expires.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Stored change of the news consent of an account as the queries read it.
/// </summary>
public sealed class NewsConsentChangeRow
{
    /// <summary>Gets the database sequence, which orders changes made at the same instant.</summary>
    public long Id { get; init; }

    /// <summary>Gets the identifier of the account.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets a value indicating whether the consent was given or withdrawn.</summary>
    public bool Granted { get; init; }

    /// <summary>Gets where the change was made, as stored.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Gets when the change was made.</summary>
    public DateTimeOffset ChangedAt { get; init; }
}
