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
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to return to email as the second factor.
/// </summary>
/// <param name="CurrentPassword">Password of the signed-in user, re-entered to authorize the change.</param>
/// <param name="Code">Current code of the authenticator.</param>
public sealed record DisableAuthenticatorCommand(
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword,
    [property: Required, MaxLength(16), NotBlank] string Code
) : ICommand<Result>;

/// <summary>
/// Executes the command that removes the authenticator. Downgrading the second factor requires
/// both the password and a current authenticator code, so neither a stolen session nor a stolen
/// password is enough; wrong codes count towards the same lockout as logins, checked with the
/// account row locked so parallel attempts are counted one after another.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DisableAuthenticatorCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    IUnitOfWork uow,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options,
    ILogger<DisableAuthenticatorCommandHandler> logger
) : ICommandHandler<DisableAuthenticatorCommand, Result>
{
    /// <summary>
    /// Handles the request to remove the authenticator.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DisableAuthenticatorCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdAsync(currentUser.RequiredId(), ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (user.TwoFactorMethod != TwoFactorMethod.Authenticator)
        {
            return Error.Conflict(ApplicationErrorCode.AuthenticatorNotEnabled);
        }

        if (!await passwordAttempts.VerifyReauthenticationAsync(user, command.CurrentPassword, ct))
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        var disabled = await uow.ExecuteInTransactionAsync(
            attempt => DisableLockedAsync(user, command.Code, attempt),
            ct
        );
        if (disabled.IsFailure)
        {
            return disabled;
        }
        return Result.Success();
    }

    private async Task<Result> DisableLockedAsync(User user, string code, CancellationToken ct)
    {
        if (!await users.LockAsync(user, ct))
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (user.TwoFactorMethod != TwoFactorMethod.Authenticator)
        {
            return Error.Conflict(ApplicationErrorCode.AuthenticatorNotEnabled);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ApplicationErrorCode.TwoFactorLocked);
        }

        var step = authenticatorCodes.Match(user.AuthenticatorKey, code);
        if (step is not { } matched || !user.AcceptsAuthenticatorStep(matched))
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

        user.DisableAuthenticator(now);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
