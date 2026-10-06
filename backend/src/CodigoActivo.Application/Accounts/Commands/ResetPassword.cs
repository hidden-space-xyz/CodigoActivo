using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to reset password.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
/// <param name="Otp">Code emailed to reset the password.</param>
/// <param name="NewPassword">New password.</param>
public sealed record ResetPasswordCommand(
    UserId UserId,
    [property: Required, MaxLength(128), NotBlank] string Otp,
    [property: Required, MinLength(12), MaxLength(128), NotBlank] string NewPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command to reset password.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">The hasher value.</param>
/// <param name="otpValidator">The otp validator value.</param>
public sealed class ResetPasswordCommandHandler(
    IUserRepository users,
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
    /// <returns>A task whose result indicates success or contains the application error.</returns>
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

        if (
            user.IsBlocked
            || user.IsDependent
            || !otpValidator.IsCodeValid(
                command.Otp,
                user.UsablePasswordResetCodeHash(clock.UtcNow)
            )
        )
        {
            return Error.Validation(ApplicationErrorCode.PasswordResetInvalidOrExpired);
        }

        user.ResetPassword(hasher.Hash(command.NewPassword), clock.UtcNow);

        return Result.Success();
    }
}
