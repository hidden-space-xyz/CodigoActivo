using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Emails the link that confirms the new email an account holder asked for, and stages the change
/// it confirms.
/// </summary>
/// <param name="users">Repository used to find the account that has the new address.</param>
/// <param name="codeHasher">Hasher used so the code is never stored in plaintext.</param>
/// <param name="verification">Lifetime of the code, shared with account verification.</param>
/// <param name="accountEmails">Builder and sender of account emails.</param>
/// <param name="logger">Logger used to record delivery failures.</param>
public sealed class EmailChangeLinkIssuer(
    IUserRepository users,
    IOneTimeCodeHasher codeHasher,
    AccountVerificationOptions verification,
    AccountEmails accountEmails,
    ILogger<EmailChangeLinkIssuer> logger
)
{
    /// <summary>
    /// Emails a link with a fresh code to the new address of <paramref name="change"/> and, once
    /// it is queued, stages the change on the account: everything but the email applies at once,
    /// and the email waits for the code. When the address belongs to an account that
    /// <see cref="User.OwnsEmail"/>, its holder gets a notice instead and the code reaches nobody,
    /// so the change can never be confirmed; the outcome is otherwise the same, so the caller
    /// cannot tell whether the address has an account. Nothing is staged when the mail cannot be
    /// sent. The caller commits.
    /// </summary>
    /// <param name="user">Account the holder edits.</param>
    /// <param name="change">Change planned for the account; it must replace the email.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    /// <exception cref="ArgumentException">The change keeps the email.</exception>
    public async Task<Result> IssueAsync(
        User user,
        ProfileChange change,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var newEmail =
            change.NewEmail
            ?? throw new ArgumentException("The profile change keeps the email.", nameof(change));
        var code = AccountTokens.Create();
        var holder = await users.GetByEmailAsync(newEmail, ct);
        try
        {
            await (
                holder is { OwnsEmail: true }
                    ? accountEmails.SendEmailInUseNoticeAsync(holder, ct)
                    : accountEmails.SendEmailChangeConfirmationAsync(user, newEmail, code, ct)
            );
        }
        catch (EmailRateLimitedException)
        {
            return Error.Conflict(ApplicationErrorCode.OtpResendCooldownActive);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.AccountVerification, ex);
            return Error.Conflict(ApplicationErrorCode.EmailSendFailed);
        }

        user.ApplyProfileChangeConfirmingEmail(
            change,
            codeHasher.Hash(code),
            now,
            verification.OtpLifetime
        );
        return Result.Success();
    }
}
