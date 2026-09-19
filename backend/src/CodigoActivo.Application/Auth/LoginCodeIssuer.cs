using System.Globalization;
using System.Security.Cryptography;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Generates, emails and stores the one-time codes of email-based second-factor challenges.
/// </summary>
/// <param name="hasher">Hasher used so the code is never stored in plaintext.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="accountEmails">Builder and sender of account emails.</param>
/// <param name="logger">Logger used to record delivery failures.</param>
public sealed class LoginCodeIssuer(
    IPasswordHasher hasher,
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

    private async Task<Result> IssueCoreAsync(
        User user,
        DateTimeOffset now,
        Func<User, string, CancellationToken, Task> send,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Error.Conflict(ErrorCode.UserContactInfoRequired);
        }

        var code = GenerateCode();
        try
        {
            await send(user, code, ct);
        }
        catch (EmailRateLimitedException)
        {
            return Error.Conflict(ErrorCode.TwoFactorResendCooldownActive);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to send the one-time code email for user {UserId}",
                user.Id
            );
            return Error.Conflict(ErrorCode.EmailSendFailed);
        }

        user.IssueLoginCode(hasher.Hash(code), now, options.ChallengeLifetime);
        return Result.Success();
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator
            .GetInt32(CodeSpace)
            .ToString($"D{CodeDigits}", CultureInfo.InvariantCulture);
    }
}
