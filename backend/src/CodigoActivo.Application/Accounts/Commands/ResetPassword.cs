using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to reset the password of an account.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
/// <param name="Otp">Code from the password reset link emailed to the account.</param>
/// <param name="NewPassword">New password.</param>
public sealed record ResetPasswordCommand(
    UserId UserId,
    [property: Required, MaxLength(128), NotBlank] string Otp,
    [property: Required, MinLength(12), MaxLength(128), NotBlank] string NewPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command to reset password. The new password is hashed before the account is
/// locked and reloaded, and the code is checked again on the reloaded account, so a concurrent
/// password step or code request waits for the reset instead of making it fail. The reset spends
/// the code, lifts the lock left by wrong passwords and closes any open second-factor challenge;
/// once it is committed every session of the account ends and the holder is emailed a security
/// alert.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountLock">Runs the reset with the account locked.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">Hasher of the new password.</param>
/// <param name="otpValidator">Validator of the code from the link.</param>
public sealed class ResetPasswordCommandHandler(
    IUserRepository users,
    AccountLock accountLock,
    IClock clock,
    IPasswordHasher hasher,
    OtpValidator otpValidator
) : ICommandHandler<ResetPasswordCommand, Result>
{
    /// <summary>
    /// Handles the request to reset password.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result indicates success, <see cref="ApplicationErrorCode.UserNotFound"/>, or
    /// <see cref="ApplicationErrorCode.PasswordResetInvalidOrExpired"/> for a wrong, expired or
    /// spent code or a blocked or dependent account.
    /// </returns>
    public async Task<Result> HandleAsync(
        ResetPasswordCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (!MayReset(user, command.Otp))
        {
            return Invalid();
        }

        var newHash = hasher.Hash(command.NewPassword);
        return await accountLock.RunAsync(
            user,
            _ => Task.FromResult(ResetLocked(user, command.Otp, newHash)),
            Error.NotFound(ApplicationErrorCode.UserNotFound),
            ct
        );
    }

    private static Result Invalid()
    {
        return Error.Validation(ApplicationErrorCode.PasswordResetInvalidOrExpired);
    }

    private bool MayReset(User user, string code)
    {
        return !user.IsBlocked
            && !user.IsDependent
            && otpValidator.IsCodeValid(code, user.PasswordResetCode.UsableHash(clock.UtcNow));
    }

    private Result ResetLocked(User user, string code, string newHash)
    {
        if (!MayReset(user, code))
        {
            return Invalid();
        }

        user.ResetPassword(newHash, clock.UtcNow);
        return Result.Success();
    }
}
