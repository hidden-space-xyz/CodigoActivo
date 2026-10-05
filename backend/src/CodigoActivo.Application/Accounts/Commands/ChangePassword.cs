using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to change password.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record ChangePasswordCommand(Guid UserId, ChangePasswordRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command to change password.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="hasher">The hasher value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="sessions">Repository used to revoke the open sessions of the user.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
public sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IClock clock,
    IUnitOfWork uow,
    IUserSessionRepository sessions,
    PasswordAttemptGuard passwordAttempts,
    AccountSecurityNotifier securityNotifier
) : ICommandHandler<ChangePasswordCommand, Result>
{
    /// <summary>
    /// Handles the request to change password.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return Error.Validation(ErrorCode.UserPasswordNotSet);
        }

        if (
            string.Equals(
                command.Request.NewPassword,
                command.Request.CurrentPassword,
                StringComparison.Ordinal
            )
        )
        {
            return Error.Validation(ErrorCode.UserNewPasswordSameAsCurrent);
        }

        if (
            !await passwordAttempts.VerifyReauthenticationAsync(
                user,
                command.Request.CurrentPassword,
                ct
            )
        )
        {
            return Error.Validation(ErrorCode.UserCurrentPasswordIncorrect);
        }

        user.ResetPassword(hasher.Hash(command.Request.NewPassword), clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        await sessions.EndAllAsync(user.Id, ct);
        await securityNotifier.NotifyAsync(user, AccountSecurityChange.PasswordChanged, ct);
        return Result.Success();
    }
}
