using CodigoActivo.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Declares every security event the application handlers emit. Templates only ever take entity
/// identifiers, enum values and counts: never names, addresses, identifiers typed by the client,
/// passwords, hashes, codes or tokens. Successful logins are deliberately absent.
/// </summary>
internal static partial class SecurityLog
{
    /// <summary>
    /// Records that the password step was attempted for an identifier that matches no account.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Login password step failed for an unknown identifier"
    )]
    public static partial void LoginUnknownIdentifierRejected(this ILogger logger);

    /// <summary>
    /// Records that the password step was rejected for a known account.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Login password step failed for user {UserId}"
    )]
    public static partial void LoginPasswordRejected(this ILogger logger, Guid userId);

    /// <summary>
    /// Records that a correct password did not open a challenge because of the account status.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="userStatusTypeId">Identifier of the user status type.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Login refused for user {UserId} because the account status {UserStatusTypeId} cannot sign in"
    )]
    public static partial void LoginRefusedForAccountStatus(
        this ILogger logger,
        Guid userId,
        Guid userStatusTypeId
    );

    /// <summary>
    /// Records that an operation was refused while the second factor was locked.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="operation">Name of the use case that was refused.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Operation {Operation} refused for user {UserId} because the second factor is locked"
    )]
    public static partial void TwoFactorLockoutBlocked(
        this ILogger logger,
        Guid userId,
        string operation
    );

    /// <summary>
    /// Records a wrong second-factor code.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="twoFactorMethod">Second factor the code was checked against.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Second-factor code rejected for user {UserId} using method {TwoFactorMethod}"
    )]
    public static partial void TwoFactorCodeRejected(
        this ILogger logger,
        Guid userId,
        TwoFactorMethod twoFactorMethod
    );

    /// <summary>
    /// Records that the wrong codes reached the limit and locked the second factor.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="maxFailedAttempts">Failures allowed before locking.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Second factor locked for user {UserId} after {MaxFailedAttempts} wrong codes"
    )]
    public static partial void TwoFactorLockoutTriggered(
        this ILogger logger,
        Guid userId,
        int maxFailedAttempts
    );

    /// <summary>
    /// Records a wrong code while confirming an authenticator enrollment.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Authenticator enrollment code rejected for user {UserId}"
    )]
    public static partial void AuthenticatorEnrollmentCodeRejected(this ILogger logger, Guid userId);

    /// <summary>
    /// Records that a route asking for the caller's own password got a wrong or missing one.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="actingUserId">Identifier of the acting user.</param>
    /// <param name="operation">Name of the use case that asked for the password.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Re-authentication rejected for user {ActingUserId} during {Operation}"
    )]
    public static partial void ReauthenticationRejected(
        this ILogger logger,
        Guid actingUserId,
        string operation
    );

    /// <summary>
    /// Records a wrong or expired password-reset code for a known account.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="userStatusTypeId">Identifier of the user status type.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Password-reset code rejected for user {UserId} with account status {UserStatusTypeId}"
    )]
    public static partial void PasswordResetCodeRejected(
        this ILogger logger,
        Guid userId,
        Guid userStatusTypeId
    );

    /// <summary>
    /// Records a wrong or expired account-verification code for a known account.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="userStatusTypeId">Identifier of the user status type.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Account verification code rejected for user {UserId} with account status {UserStatusTypeId}"
    )]
    public static partial void AccountVerificationCodeRejected(
        this ILogger logger,
        Guid userId,
        Guid userStatusTypeId
    );

    /// <summary>
    /// Records that an account replaced its password after proving the previous one.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Password changed for user {UserId}")]
    public static partial void PasswordChanged(this ILogger logger, Guid userId);

    /// <summary>
    /// Records that an account replaced its password through the recovery flow.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Password reset completed for user {UserId}"
    )]
    public static partial void PasswordResetCompleted(this ILogger logger, Guid userId);

    /// <summary>
    /// Records that an authenticator application became the second factor.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Authenticator enrolled for user {UserId}")]
    public static partial void AuthenticatorEnrolled(this ILogger logger, Guid userId);

    /// <summary>
    /// Records that the authenticator was removed and email became the second factor again.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Authenticator removed for user {UserId}")]
    public static partial void AuthenticatorRemoved(this ILogger logger, Guid userId);

    /// <summary>
    /// Records that an administrator returned somebody's second factor to email.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="actingUserId">Identifier of the acting user.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Second factor reset by administrator {ActingUserId} for user {UserId}"
    )]
    public static partial void TwoFactorResetByAdministrator(
        this ILogger logger,
        Guid actingUserId,
        Guid userId
    );

    /// <summary>
    /// Records that the administrator flag of an account was granted or revoked.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="actingUserId">Identifier of the acting user.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="isAdmin">State the flag was set to.</param>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Administrator flag set to {IsAdmin} by user {ActingUserId} for user {UserId}"
    )]
    public static partial void AdministratorFlagChanged(
        this ILogger logger,
        Guid actingUserId,
        Guid userId,
        bool isAdmin
    );

    /// <summary>
    /// Records that the email or phone an account signs in with was replaced.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="actingUserId">Identifier of the acting user.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Login identifiers changed by user {ActingUserId} for user {UserId}"
    )]
    public static partial void LoginIdentifiersChanged(
        this ILogger logger,
        Guid actingUserId,
        Guid userId
    );

    /// <summary>
    /// Records that an account erased itself after the password and the second factor.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Account deleted by its own owner {UserId}"
    )]
    public static partial void AccountDeletedByOwner(this ILogger logger, Guid userId);

    /// <summary>
    /// Records that an administrator or guardian deleted somebody else's account.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="actingUserId">Identifier of the acting user.</param>
    /// <param name="userId">Identifier of the user.</param>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "User {UserId} deleted by user {ActingUserId}"
    )]
    public static partial void UserDeletedByAnotherUser(
        this ILogger logger,
        Guid actingUserId,
        Guid userId
    );

    /// <summary>
    /// Records that an administrator moved an account to another user type.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="userTypeId">Identifier of the user type.</param>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "User {UserId} moved to user type {UserTypeId} by an administrator"
    )]
    public static partial void UserTypeChanged(this ILogger logger, Guid userId, Guid userTypeId);
}
