using CodigoActivo.Application.Accounts.Notifications;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Declares the security events the application keeps. Routine authentication outcomes are not
/// recorded: only the lockouts that stop an account and the security notification the limiter
/// dropped are. No template carries an identifier, an address, a name or anything typed by the
/// client, so no entry can be traced back to a person.
/// </summary>
public static partial class SecurityLog
{
    /// <summary>
    /// Records that the wrong passwords reached the limit and locked an account. Only a completed
    /// password reset lifts the lock.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="maxFailedAttempts">Failures allowed before locking.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "An account was locked after {MaxFailedAttempts} wrong passwords"
    )]
    public static partial void PasswordLockoutTriggered(this ILogger logger, int maxFailedAttempts);

    /// <summary>
    /// Records that the wrong codes reached the limit and locked the second factor of an account.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="maxFailedAttempts">Failures allowed before locking.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The second factor of an account was locked after {MaxFailedAttempts} wrong codes"
    )]
    public static partial void TwoFactorLockoutTriggered(
        this ILogger logger,
        int maxFailedAttempts
    );
}
