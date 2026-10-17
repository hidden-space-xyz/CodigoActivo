using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to resend verification.
/// </summary>
/// <param name="Email">Email of the account.</param>
public sealed record ResendVerificationCommand(
    [property: Required, EmailAddress, MaxLength(256)] string Email
) : ICommand<Result>;

/// <summary>
/// Executes the command to resend verification.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountLock">Runs the issue of the link with the account locked.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="codeHasher">Hasher used so the verification code is never stored in plaintext.</param>
/// <param name="verification">The verification value.</param>
/// <param name="accountEmails">The account emails value.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
/// <param name="auditSubject">Names the account the request found for the audit log.</param>
public sealed class ResendVerificationCommandHandler(
    IUserRepository users,
    AccountLock accountLock,
    IClock clock,
    IOneTimeCodeHasher codeHasher,
    AccountVerificationOptions verification,
    AccountEmails accountEmails,
    ILogger<ResendVerificationCommandHandler> logger,
    IAuditSubject auditSubject
) : ICommandHandler<ResendVerificationCommand, Result>
{
    /// <summary>
    /// Handles the request to resend verification. A new link reaches the address only when it
    /// belongs to an account waiting for verification whose last link is older than the resend
    /// cooldown; every request succeeds alike, so the answer never tells whether the address has
    /// an account. Only an account that will get a link is locked, so an address that gets nothing
    /// costs the same as one without an account; the link is issued with the account locked and
    /// its eligibility checked again, so parallel requests run one after another and the later ones
    /// find the resend cooldown.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result always reports success.</returns>
    public async Task<Result> HandleAsync(
        ResendVerificationCommand command,
        CancellationToken ct = default
    )
    {
        var email = EmailAddress.Create(command.Email);
        var user = email.IsSuccess ? await users.GetByEmailAsync(email.Value, ct) : null;
        if (user is not null)
        {
            auditSubject.Name(user.Id);
        }

        return user is not null && MayResend(user)
            ? await accountLock.RunAsync(
                user,
                attempt => IssueLockedAsync(user, attempt),
                Result.Success(),
                ct
            )
            : Result.Success();
    }

    private bool MayResend(User user)
    {
        return user.IsPendingVerification
            && !user.VerificationCode.IsResendCoolingDown(
                clock.UtcNow,
                verification.ResendCooldown
            );
    }

    private async Task<Result> IssueLockedAsync(User user, CancellationToken ct)
    {
        if (!MayResend(user))
        {
            return Result.Success();
        }

        var now = clock.UtcNow;

        var otpCode = AccountTokens.Create();
        try
        {
            await accountEmails.SendVerificationEmailAsync(user, otpCode, ct);
        }
        catch (EmailRateLimitedException)
        {
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.AccountVerification, ex);
            return Result.Success();
        }

        user.IssueOtp(codeHasher.Hash(otpCode), now, verification.OtpLifetime);
        return Result.Success();
    }
}
