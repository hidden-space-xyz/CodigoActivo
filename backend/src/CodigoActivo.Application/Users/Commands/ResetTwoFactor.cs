using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required by an administrator to reset a user's second factor.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="CurrentPassword">Password of the signed-in administrator, re-entered to authorize the reset.</param>
public sealed record ResetTwoFactorCommand(
    UserId UserId,
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command to reset a user's second factor to email: it drops the authenticator, the
/// pending login code and the open login challenge, and lifts the second-factor lock. The holder is
/// told by email only when the account used an authenticator.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ResetTwoFactorCommandHandler(
    IUserRepository users,
    PasswordAttemptGuard passwordAttempts,
    IClock clock
) : ICommandHandler<ResetTwoFactorCommand, Result>
{
    /// <summary>
    /// Handles the request to reset a user's second factor.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.UserCurrentPasswordIncorrect"/> when the administrator's
    /// password is wrong or locked, or <see cref="ApplicationErrorCode.UserNotFound"/>.
    /// </returns>
    public async Task<Result> HandleAsync(
        ResetTwoFactorCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await passwordAttempts.VerifyActingUserAsync(command.CurrentPassword, ct))
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        user.ResetTwoFactor(clock.UtcNow);
        return Result.Success();
    }
}
