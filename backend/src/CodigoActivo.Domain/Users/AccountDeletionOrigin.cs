namespace CodigoActivo.Domain.Users;

/// <summary>
/// Identifies who asked for an account to be deleted.
/// </summary>
public enum AccountDeletionOrigin
{
    /// <summary>
    /// The account holder deleted their own account.
    /// </summary>
    Self = 1,

    /// <summary>
    /// An administrator deleted somebody else's account.
    /// </summary>
    Administrator = 2,

    /// <summary>
    /// A guardian deleted one of their dependents.
    /// </summary>
    Guardian = 3,

    /// <summary>
    /// Another account claimed the email of this account, which nobody had verified.
    /// </summary>
    EmailClaimed = 4,

    /// <summary>
    /// Nobody verified the account within <see cref="User.UnverifiedRetentionDays"/> of its
    /// registration, so it was erased on schedule.
    /// </summary>
    VerificationExpired = 5,

    /// <summary>
    /// The dependent came of age, so its guardian no longer represents it, and it was erased on
    /// schedule.
    /// </summary>
    CameOfAge = 6,
}
