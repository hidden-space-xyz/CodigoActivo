using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.SecondFactor;
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
/// Executes the command to verify user. The code is checked with the account locked and reloaded,
/// so a verification that runs alongside a new link or a registration claiming the address sees
/// what the other one stored.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountLock">Runs the verification with the account locked.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="otpValidator">The otp validator value.</param>
public sealed class VerifyUserCommandHandler(
    IUserRepository users,
    AccountLock accountLock,
    IClock clock,
    OtpValidator otpValidator
) : ICommandHandler<VerifyUserCommand, Result>
{
    private static readonly Result NotFound = Error.NotFound(ApplicationErrorCode.UserNotFound);

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
            return NotFound;
        }

        return RefusalOf(user, command.Otp)
            ?? await accountLock.RunAsync(
                user,
                _ => Task.FromResult(VerifyLocked(user, command.Otp)),
                NotFound,
                ct
            );
    }

    private Result? RefusalOf(User user, string code)
    {
        if (!otpValidator.IsCodeValid(code, user.VerificationCode.UsableHash(clock.UtcNow)))
        {
            return Error.Validation(ApplicationErrorCode.OtpInvalidOrExpired);
        }

        return null;
    }

    private Result VerifyLocked(User user, string code)
    {
        return RefusalOf(user, code) ?? user.Verify(clock.UtcNow);
    }
}
