using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
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
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="codeHasher">Hasher used so the verification code is never stored in plaintext.</param>
/// <param name="verification">The verification value.</param>
/// <param name="accountEmails">The account emails value.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ResendVerificationCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IOneTimeCodeHasher codeHasher,
    AccountVerificationOptions verification,
    AccountEmails accountEmails,
    ILogger<ResendVerificationCommandHandler> logger
) : ICommandHandler<ResendVerificationCommand, Result>
{
    /// <summary>
    /// Handles the request to resend verification. A new link reaches the address only when it
    /// belongs to an account waiting for verification whose last link is older than the resend
    /// cooldown; every request succeeds alike, so the answer never tells whether the address has
    /// an account.
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
        var now = clock.UtcNow;
        if (
            user is not { IsPendingVerification: true }
            || user.IsOtpResendCoolingDown(now, verification.ResendCooldown)
        )
        {
            return Result.Success();
        }

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
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
