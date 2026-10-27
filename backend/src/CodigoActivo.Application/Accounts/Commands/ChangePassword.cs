using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to change password.
/// </summary>
/// <param name="UserId">Identifier of the account; it must be the signed-in user's own.</param>
/// <param name="CurrentPassword">Current password of the account.</param>
/// <param name="NewPassword">New password; it must differ from the current one.</param>
public sealed record ChangePasswordCommand(
    UserId UserId,
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword,
    [property: Required, MinLength(12), MaxLength(128), NotBlank] string NewPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command to change password. The new password is hashed before the account is
/// locked and reloaded; the change then applies only while the stored password is still the one
/// the current password was checked against, so a concurrent password step or code request waits
/// for it instead of making it fail. Only the holder changes their own password: it needs the
/// current one, which nobody else should know, and a wrong guess counts toward the account's lock.
/// Once the change is committed every session of the account ends and the holder is emailed a
/// security alert.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountLock">Runs the change with the account locked.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="hasher">Hasher of the new password.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
public sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    AccountLock accountLock,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    IClock clock,
    PasswordAttemptGuard passwordAttempts
) : ICommandHandler<ChangePasswordCommand, Result>
{
    /// <summary>
    /// Handles the request to change password.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result indicates success,
    /// <see cref="ApplicationErrorCode.ActingForAnotherUserForbidden"/> for another account,
    /// <see cref="ApplicationErrorCode.UserPasswordNotSet"/>,
    /// <see cref="ApplicationErrorCode.UserNewPasswordSameAsCurrent"/>, or
    /// <see cref="ApplicationErrorCode.UserCurrentPasswordIncorrect"/> when the current password is
    /// wrong, was replaced meanwhile or the account is locked by wrong passwords.
    /// </returns>
    public async Task<Result> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.RequiredId() != command.UserId)
        {
            return Error.Forbidden(ApplicationErrorCode.ActingForAnotherUserForbidden);
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return Error.Validation(ApplicationErrorCode.UserPasswordNotSet);
        }

        if (string.Equals(command.NewPassword, command.CurrentPassword, StringComparison.Ordinal))
        {
            return Error.Validation(ApplicationErrorCode.UserNewPasswordSameAsCurrent);
        }

        if (!await passwordAttempts.VerifyReauthenticationAsync(user, command.CurrentPassword, ct))
        {
            return CurrentPasswordIncorrect();
        }

        var verifiedHash = user.PasswordHash;
        var newHash = hasher.Hash(command.NewPassword);
        return await accountLock.RunAsync(
            user,
            _ => Task.FromResult(ChangeLocked(user, verifiedHash, newHash)),
            Error.NotFound(ApplicationErrorCode.UserNotFound),
            ct
        );
    }

    private static Result CurrentPasswordIncorrect()
    {
        return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
    }

    private Result ChangeLocked(User user, string verifiedHash, string newHash)
    {
        if (!PasswordAttemptGuard.StillVerified(user, verifiedHash))
        {
            return CurrentPasswordIncorrect();
        }

        user.ChangePassword(newHash, clock.UtcNow);
        return Result.Success();
    }
}
