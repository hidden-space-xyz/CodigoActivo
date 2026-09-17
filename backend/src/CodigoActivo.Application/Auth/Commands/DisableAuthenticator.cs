using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Auth.Commands;

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
/// password is enough; wrong codes count towards the same lockout as logins.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">Hasher used to verify the current password.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
public sealed class DisableAuthenticatorCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options
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
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (user.TwoFactorMethod != TwoFactorMethod.Authenticator)
        {
            return Error.Conflict(ErrorCode.AuthenticatorNotEnabled);
        }

        if (
            string.IsNullOrEmpty(user.PasswordHash)
            || !hasher.Verify(command.Request.CurrentPassword, user.PasswordHash)
        )
        {
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ErrorCode.TwoFactorLocked);
        }

        var step = authenticatorCodes.Match(
            user.AuthenticatorKey,
            command.Request.Code,
            user.AuthenticatorLastUsedStep
        );
        if (step is null)
        {
            user.RecordTwoFactorFailure(now, options.MaxFailedAttempts, options.LockoutDuration);
            await uow.SaveChangesAsync(ct);
            return Error.BadRequest(ErrorCode.TwoFactorCodeInvalid);
        }

        user.UseEmailTwoFactor(now);
        user.TwoFactorFailedAttempts = 0;
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
