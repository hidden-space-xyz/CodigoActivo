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
/// Carries the input required to change password.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
/// <param name="CurrentPassword">Current password of the account.</param>
/// <param name="NewPassword">New password.</param>
public sealed record ChangePasswordCommand(
    UserId UserId,
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword,
    [property: Required, MinLength(12), MaxLength(128), NotBlank] string NewPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command to change password.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="actingUser">Policy that decides for whom the signed-in user may act.</param>
/// <param name="hasher">The hasher value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
public sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    ActingUserPolicy actingUser,
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
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var allowed = await actingUser.EnsureMayActForAsync(command.UserId, ct);
        if (allowed.IsFailure)
        {
            return allowed;
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
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        user.ChangePassword(hasher.Hash(command.NewPassword), clock.UtcNow);
        return Result.Success();
    }
}
