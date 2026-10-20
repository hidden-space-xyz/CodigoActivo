using System.Globalization;
using System.Security.Cryptography;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.SecondFactor;

/// <summary>
/// Generates, emails and stores the one-time codes of email-based second-factor challenges.
/// </summary>
/// <param name="codeHasher">Hasher used so the code is never stored in plaintext.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="accountEmails">Builder and sender of account emails.</param>
/// <param name="logger">Logger used to record delivery failures.</param>
public sealed class LoginCodeIssuer(
    IOneTimeCodeHasher codeHasher,
    TwoFactorOptions options,
    AccountEmails accountEmails,
    ILogger<LoginCodeIssuer> logger
)
{
    private const int CodeDigits = 6;
    private static readonly int CodeSpace = (int)Math.Pow(10, CodeDigits);

    /// <summary>
    /// Emails a fresh code to the user and stages its hash on the entity. The caller commits.
    /// </summary>
    /// <param name="user">User whose challenge receives the code.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public Task<Result> IssueAsync(User user, DateTimeOffset now, CancellationToken ct)
    {
        return IssueCoreAsync(user, now, accountEmails.SendLoginCodeEmailAsync, ct);
    }

    /// <summary>
    /// Emails a fresh code that confirms deleting the account and stages its hash on the entity.
    /// The code lives in the same fields as the login code, so it shares its lifetime, its resend
    /// cooldown and the second-factor lockout. The caller commits.
    /// </summary>
    /// <param name="user">User whose account deletion receives the code.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public Task<Result> IssueAccountDeletionAsync(
        User user,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        return IssueCoreAsync(user, now, accountEmails.SendAccountDeletionCodeEmailAsync, ct);
    }

    /// <summary>
    /// Tells why a new code may not be emailed to the user now: the account may not receive codes
    /// (<see cref="User.MayReceiveLoginCode"/>) or the last one was sent within the resend cooldown.
    /// </summary>
    /// <param name="user">User who asks for a new code.</param>
    /// <param name="now">Current timestamp.</param>
    /// <returns>The failed result, or <see langword="null"/> when a new code may be sent.</returns>
    public Result? RefusalOf(User user, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);

        var allowed = user.MayReceiveLoginCode(now);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        if (user.LoginCode.IsResendCoolingDown(now, options.ResendCooldown))
        {
            return Error.Conflict(ApplicationErrorCode.TwoFactorResendCooldownActive);
        }

        return null;
    }

    private async Task<Result> IssueCoreAsync(
        User user,
        DateTimeOffset now,
        Func<User, string, CancellationToken, Task> send,
        CancellationToken ct
    )
    {
        var allowed = user.MayReceiveLoginCode(now);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var code = GenerateCode();
        try
        {
            await send(user, code, ct);
        }
        catch (EmailRateLimitedException)
        {
            return Error.Conflict(ApplicationErrorCode.TwoFactorResendCooldownActive);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.TwoFactorCode, ex);
            return Error.Conflict(ApplicationErrorCode.EmailSendFailed);
        }

        return user.IssueLoginCode(codeHasher.Hash(code), now, options.ChallengeLifetime);
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator
            .GetInt32(CodeSpace)
            .ToString($"D{CodeDigits}", CultureInfo.InvariantCulture);
    }
}
