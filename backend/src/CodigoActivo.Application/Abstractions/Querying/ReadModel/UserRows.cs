using CodigoActivo.Domain.Users;

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

    /// <summary>Gets a value indicating whether promotional email is accepted.</summary>
    public bool PromotionalConsent { get; init; }

    /// <summary>Gets the gender.</summary>
    public Gender Gender { get; init; }

    /// <summary>Gets the last successful login.</summary>
    public DateTimeOffset? LastLoginAt { get; init; }

    /// <summary>Gets when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the account was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

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

    /// <summary>Gets when the pending verification code expires.</summary>
    public DateTimeOffset? OtpExpiresAt { get; init; }

    /// <summary>Gets when the last verification code was sent.</summary>
    public DateTimeOffset? OtpLastSentAt { get; init; }

    /// <summary>Gets when the pending password reset code expires.</summary>
    public DateTimeOffset? PasswordResetExpiresAt { get; init; }

    /// <summary>Gets when the last password reset code was sent.</summary>
    public DateTimeOffset? PasswordResetLastSentAt { get; init; }

    /// <summary>Gets the second-factor method.</summary>
    public TwoFactorMethod TwoFactorMethod { get; init; } = TwoFactorMethod.Email;

    /// <summary>Gets the last authenticator step accepted.</summary>
    public long? AuthenticatorLastUsedStep { get; init; }

    /// <summary>Gets when the pending authenticator enrollment expires.</summary>
    public DateTimeOffset? PendingAuthenticatorExpiresAt { get; init; }

    /// <summary>Gets when the pending login code expires.</summary>
    public DateTimeOffset? LoginCodeExpiresAt { get; init; }

    /// <summary>Gets when the last login code was sent.</summary>
    public DateTimeOffset? LoginCodeLastSentAt { get; init; }

    /// <summary>Gets the identifier of the pending second-factor challenge.</summary>
    public Guid? LoginChallengeId { get; init; }

    /// <summary>Gets the consecutive failed second-factor attempts.</summary>
    public int TwoFactorFailedAttempts { get; init; }

    /// <summary>Gets until when the second factor is locked.</summary>
    public DateTimeOffset? TwoFactorLockedUntil { get; init; }

    /// <summary>Gets the consecutive wrong passwords.</summary>
    public int PasswordFailedAttempts { get; init; }

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
