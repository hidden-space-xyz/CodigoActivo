using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth.Commands;

/// <summary>
/// Carries the input required to resend verification.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
public sealed record ResendVerificationCommand(Guid UserId) : ICommand<Result>;

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
    /// Handles the request to resend verification.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ResendVerificationCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            user.UserStatusTypeId != SeedIds.UserStatusTypes.Pending
            || string.IsNullOrWhiteSpace(user.Email)
        )
        {
            return Error.Conflict(ErrorCode.OtpResendNotAllowed);
        }

        var now = clock.UtcNow;
        if (now < user.OtpLastSentAt + verification.ResendCooldown)
        {
            return Error.Conflict(ErrorCode.OtpResendCooldownActive);
        }

        var otpCode = AccountTokens.Create();
        try
        {
            await accountEmails.SendVerificationEmailAsync(user, otpCode, ct);
        }
        catch (EmailRateLimitedException)
        {
            return Error.Conflict(ErrorCode.OtpResendCooldownActive);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to send the verification email for user {UserId}", user.Id);
            return Error.Conflict(ErrorCode.EmailSendFailed);
        }

        user.IssueOtp(hasher.Hash(otpCode), now, verification.OtpLifetime);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
