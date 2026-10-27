namespace CodigoActivo.Application.Accounts.Passwords;

/// <summary>
/// Defines configuration values for the account lock that follows repeated wrong passwords.
/// </summary>
public sealed class PasswordLockoutOptions
{
    /// <summary>
    /// Identifies the default number of wrong passwords tolerated before the account locks.
    /// </summary>
    public const int DefaultMaxFailedAttempts = 5;

    /// <summary>
    /// Gets or sets the wrong passwords tolerated before the account locks. The lock has no
    /// duration: regaining access requires completing a password reset.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = DefaultMaxFailedAttempts;
}
