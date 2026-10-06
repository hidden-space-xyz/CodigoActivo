using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to verify user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Otp">The otp value.</param>
public sealed record VerifyUserCommand(
    UserId UserId,
    [property: Required, MaxLength(128), NotBlank] string Otp
) : ICommand<Result>;

/// <summary>
/// Executes the command to verify user.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="otpValidator">The otp validator value.</param>
public sealed class VerifyUserCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    OtpValidator otpValidator
) : ICommandHandler<VerifyUserCommand, Result>
{
    /// <summary>
    /// Handles the request to verify user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(VerifyUserCommand command, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var now = clock.UtcNow;
        if (
            !user.IsPendingVerification
            || !otpValidator.IsCodeValid(command.Otp, user.UsableOtpCodeHash(now))
        )
        {
            return Error.Validation(ApplicationErrorCode.OtpInvalidOrExpired);
        }

        user.Verify(now);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
