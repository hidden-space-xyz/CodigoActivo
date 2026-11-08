namespace CodigoActivo.Application.Accounts.Passwords;

/// <summary>
/// Defines configuration values for the temporary account lock that follows repeated wrong
/// passwords.
/// </summary>
public sealed class PasswordLockoutOptions
{
    /// <summary>
    /// Identifies the default number of wrong passwords tolerated before the account locks.
    /// </summary>
    public const int DefaultMaxFailedAttempts = 5;

    /// <summary>
    /// Gets or sets the wrong passwords tolerated before the account locks.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = DefaultMaxFailedAttempts;

    /// <summary>
    /// Gets or sets how long the account refuses every password after too many wrong ones. A
    /// password reset lifts the lock earlier.
    /// </summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
