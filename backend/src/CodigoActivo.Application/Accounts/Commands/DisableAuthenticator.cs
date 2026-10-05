using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to return to email as the second factor.
/// </summary>
/// <param name="UserId">Identifier of the signed-in user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record DisableAuthenticatorCommand(Guid UserId, DisableAuthenticatorRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command that removes the authenticator. Downgrading the second factor requires
/// both the password and a current authenticator code, so neither a stolen session nor a stolen
/// password is enough; wrong codes count towards the same lockout as logins, checked with the
/// account row locked so parallel attempts are counted one after another.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DisableAuthenticatorCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options,
    AccountSecurityNotifier securityNotifier,
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
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (user.TwoFactorMethod != TwoFactorMethod.Authenticator)
        {
            return Error.Conflict(ErrorCode.AuthenticatorNotEnabled);
        }

        if (
            !await passwordAttempts.VerifyReauthenticationAsync(
                user,
                command.Request.CurrentPassword,
                ct
            )
        )
        {
            return Error.Validation(ErrorCode.UserCurrentPasswordIncorrect);
        }

        var disabled = await uow.ExecuteInTransactionAsync(
            attempt => DisableLockedAsync(user, command.Request.Code, attempt),
            ct
        );
        if (disabled.IsFailure)
        {
            return disabled;
        }

        await securityNotifier.NotifyAsync(user, AccountSecurityChange.AuthenticatorDisabled, ct);
        return Result.Success();
    }

    private async Task<Result> DisableLockedAsync(User user, string code, CancellationToken ct)
    {
        if (!await users.LockAsync(user, ct))
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (user.TwoFactorMethod != TwoFactorMethod.Authenticator)
        {
            return Error.Conflict(ErrorCode.AuthenticatorNotEnabled);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ErrorCode.TwoFactorLocked);
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

            return Error.Validation(ErrorCode.TwoFactorCodeInvalid);
        }

        user.UseEmailTwoFactor(now);
        user.ClearTwoFactorFailures();
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
