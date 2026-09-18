using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth.Commands;

/// <summary>
/// Carries the input required to reset password.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record ResetPasswordCommand(Guid UserId, ResetPasswordRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command to reset password.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">The hasher value.</param>
/// <param name="otpValidator">The otp validator value.</param>
/// <param name="sessions">Repository used to revoke the open sessions of the user.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ResetPasswordCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    OtpValidator otpValidator,
    IUserSessionRepository sessions,
    AccountSecurityNotifier securityNotifier,
    ILogger<ResetPasswordCommandHandler> logger
) : ICommandHandler<ResetPasswordCommand, Result>
{
    /// <summary>
    /// Handles the request to reset password.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ResetPasswordCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            user.UserStatusTypeId == SeedIds.UserStatusTypes.Blocked
            || user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
            || !otpValidator.IsCodeValid(
                request.Otp,
                user.PasswordResetCodeHash,
                user.PasswordResetExpiresAt
            )
        )
        {
            logger.PasswordResetCodeRejected(user.Id, user.UserStatusTypeId);
            return Error.BadRequest(ErrorCode.PasswordResetInvalidOrExpired);
        }

        user.ResetPassword(hasher.Hash(request.NewPassword), clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        await sessions.RemoveAsync(session => session.UserId == user.Id, ct);
        logger.PasswordResetCompleted(user.Id);
        await securityNotifier.NotifyAsync(user, AccountSecurityChange.PasswordReset, ct);

        return Result.Success();
    }
}
