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
/// Carries the input required to verify the email of a new account.
/// </summary>
/// <param name="UserId">Identifier of the account waiting for its verification.</param>
/// <param name="Otp">Code from the verification link emailed to the account.</param>
public sealed record VerifyUserCommand(
    UserId UserId,
    [property: Required, MaxLength(128), NotBlank] string Otp
) : ICommand<Result>;

/// <summary>
/// Executes the command that activates an account once the code of its verification link is
/// accepted. The code is checked with the account locked and reloaded, so a verification that runs
/// alongside a new link or a registration claiming the address sees what the other one stored.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountLock">Runs the verification with the account locked.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="otpValidator">Validator of the code from the link.</param>
public sealed class VerifyUserCommandHandler(
    IUserRepository users,
    AccountLock accountLock,
    IClock clock,
    OtpValidator otpValidator
) : ICommandHandler<VerifyUserCommand, Result>
{
    private static readonly Result NotFound = Error.NotFound(ApplicationErrorCode.UserNotFound);

    /// <summary>
    /// Handles the request to verify the account.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, <see cref="ApplicationErrorCode.UserNotFound"/>, or
    /// <see cref="ApplicationErrorCode.OtpInvalidOrExpired"/> for a wrong, expired or spent code.
    /// </returns>
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
