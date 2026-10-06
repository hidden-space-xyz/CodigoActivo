using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to complete a login with the second factor.
/// </summary>
/// <param name="UserId">Identifier of the user whose password was already accepted.</param>
/// <param name="Code">Code from the email or the authenticator application.</param>
public sealed record VerifyTwoFactorLoginCommand(
    UserId UserId,
    [property: Required, MaxLength(16), NotBlank] string Code
) : ICommand<Result>;

/// <summary>
/// Executes the second step of the login. Wrong codes are counted and lock the account's second
/// factor for a while once the limit is reached, so short codes cannot be brute forced. Each check
/// runs with the account row locked and reloaded, so parallel attempts are counted one after another
/// and a code or an authenticator time step is accepted only once. An account locked after repeated
/// wrong passwords is refused like an expired challenge, even when its challenge ticket was obtained
/// before the lock, so no session is opened on a locked account.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="otpValidator">Validator of emailed codes.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class VerifyTwoFactorLoginCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    OtpValidator otpValidator,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options,
    ILogger<VerifyTwoFactorLoginCommandHandler> logger
) : ICommandHandler<VerifyTwoFactorLoginCommand, Result>
{
    /// <summary>
    /// Handles the request to complete the login.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        VerifyTwoFactorLoginCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => VerifyLockedAsync(user, command.Code, attempt),
            ct
        );
    }

    private async Task<Result> VerifyLockedAsync(User user, string code, CancellationToken ct)
    {
        if (!await users.LockAsync(user, ct))
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (user.IsPasswordLocked())
        {
            return Error.Unauthorized(ApplicationErrorCode.TwoFactorChallengeExpired);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ApplicationErrorCode.TwoFactorLocked);
        }

        long? usedStep = null;
        var accepted = user.TwoFactorMethod switch
        {
            TwoFactorMethod.Authenticator => (
                usedStep = authenticatorCodes.Match(user.AuthenticatorKey, code)
            )
                is { } matched
                && user.AcceptsAuthenticatorStep(matched),
            _ => otpValidator.IsCodeValid(code, user.UsableLoginCodeHash(now)),
        };

        if (!accepted)
        {
            var locked = user.RecordTwoFactorFailure(
                now,
                options.MaxFailedAttempts,
                options.LockoutDuration
            );
            await uow.SaveChangesAsync(ct);
            if (locked)
            {
                logger.TwoFactorLockoutTriggered(options.MaxFailedAttempts);
            }

            return Error.Validation(ApplicationErrorCode.TwoFactorCodeInvalid);
        }

        if (usedStep is { } step)
        {
            user.RecordAuthenticatorStep(step);
        }

        user.CompleteTwoFactorLogin(now);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
