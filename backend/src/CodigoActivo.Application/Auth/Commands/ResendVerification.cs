using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

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
public sealed class ResendVerificationCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    AccountVerificationOptions verification,
    AccountEmails accountEmails
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

        var otpCode = Guid.NewGuid().ToString();
        try
        {
            await accountEmails.SendVerificationEmailAsync(user, otpCode, ct);
        }
        catch (EmailRateLimitedException)
        {
            return Error.Conflict(ErrorCode.OtpResendCooldownActive);
        }

        user.IssueOtp(hasher.Hash(otpCode), now, verification.OtpLifetime);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
