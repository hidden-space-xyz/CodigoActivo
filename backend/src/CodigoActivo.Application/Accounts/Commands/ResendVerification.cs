using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to resend verification.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record ResendVerificationCommand(ResendVerificationRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command to resend verification.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">The hasher value.</param>
/// <param name="verification">The verification value.</param>
/// <param name="accountEmails">The account emails value.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ResendVerificationCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    AccountVerificationOptions verification,
    AccountEmails accountEmails,
    ILogger<ResendVerificationCommandHandler> logger
) : ICommandHandler<ResendVerificationCommand, Result>
{
    /// <summary>
    /// Handles the request to resend verification. A new link reaches the address only when it
    /// belongs to an account waiting for verification whose last link is older than the resend
    /// cooldown; every request succeeds alike and hashes a code first, so neither the answer nor
    /// its timing tells whether the address has an account.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result always reports success.</returns>
    public async Task<Result> HandleAsync(
        ResendVerificationCommand command,
        CancellationToken ct = default
    )
    {
        var otpCode = AccountTokens.Create();
        var otpCodeHash = hasher.Hash(otpCode);
        var email = command.Request.Email.NormalizeEmailOrNull();
        var user = email is null ? null : await users.GetByEmailAsync(email, ct);
        var now = clock.UtcNow;
        if (
            user is not { IsPendingVerification: true }
            || user.IsOtpResendCoolingDown(now, verification.ResendCooldown)
        )
        {
            return Result.Success();
        }

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

        user.IssueOtp(otpCodeHash, now, verification.OtpLifetime);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
