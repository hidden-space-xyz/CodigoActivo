using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required for an administrator to reset a user's second factor.
/// </summary>
/// <param name="UserId">Identifier of the user whose second factor is reset.</param>
/// <param name="ActingUserId">Identifier of the acting administrator.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record ResetTwoFactorCommand(
    Guid UserId,
    Guid ActingUserId,
    ResetTwoFactorRequest Request
) : ICommand<Result>;

/// <summary>
/// Executes the command that returns a user's second factor to email, forgetting their
/// authenticator and clearing any lockout. This is the recovery path for a lost authenticator,
/// so the administrator re-enters their own password first.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ResetTwoFactorCommandHandler(
    IUserRepository users,
    PasswordAttemptGuard passwordAttempts,
    IClock clock,
    IUnitOfWork uow,
    AccountSecurityNotifier securityNotifier,
    ILogger<ResetTwoFactorCommandHandler> logger
) : ICommandHandler<ResetTwoFactorCommand, Result>
{
    private const string Operation = "ResetTwoFactor";

    /// <summary>
    /// Handles the request to reset the second factor.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ResetTwoFactorCommand command,
        CancellationToken ct = default
    )
    {
        var actingUser = await users.FindAsync(u => u.Id == command.ActingUserId, ct);
        if (
            !await passwordAttempts.VerifyReauthenticationAsync(
                actingUser,
                command.Request.CurrentPassword,
                ct
            )
        )
        {
            logger.ReauthenticationRejected(command.ActingUserId, Operation);
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        user.ResetTwoFactor(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        logger.TwoFactorResetByAdministrator(command.ActingUserId, user.Id);
        await securityNotifier.NotifyAsync(user, AccountSecurityChange.TwoFactorReset, ct);
        return Result.Success();
    }
}
