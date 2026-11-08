using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to start enrolling an authenticator application.
/// </summary>
/// <param name="CurrentPassword">
/// Password of the signed-in user, re-entered to authorize the change.
/// </param>
/// <param name="Code">Code emailed to the account, its current second factor.</param>
public sealed record BeginAuthenticatorSetupCommand(
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword,
    [property: Required, MaxLength(16), NotBlank] string Code
) : ICommand<Result<AuthenticatorSetup>>, IPreparatoryCommand;

/// <summary>
/// Executes the command that creates a new shared secret. The user re-enters their password and a
/// code emailed to the account, its current second factor, so neither a stolen session with the
/// password nor the password alone can replace the second factor; wrong codes count towards the
/// same lockout as logins, checked with the account row locked. The secret only becomes active
/// once <see cref="ConfirmAuthenticatorCommandHandler"/> sees a code generated from it. An account
/// that already uses an authenticator is refused before its password is checked: it returns to
/// email first, which takes a current code. A new enrollment replaces any pending one and can be
/// confirmed for <see cref="TwoFactorOptions.SetupLifetime"/>.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="accountLock">Runs the enrollment with the account locked.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="secondFactor">Guard that verifies, counts and locks second-factor codes.</param>
/// <param name="totp">Generator of shared secrets.</param>
/// <param name="protector">Protector that encrypts the secret before it is stored.</param>
/// <param name="options">Second-factor configuration.</param>
public sealed class BeginAuthenticatorSetupCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    AccountLock accountLock,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
    SecondFactorGuard secondFactor,
    ITotpService totp,
    ISecretProtector protector,
    TwoFactorOptions options
) : ICommandHandler<BeginAuthenticatorSetupCommand, Result<AuthenticatorSetup>>
{
    /// <summary>
    /// Handles the request to start the enrollment.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the enrollment data, shown only now, on success;
    /// <see cref="DomainErrorCode.AuthenticatorAlreadyEnabled"/> when an authenticator is active,
    /// <see cref="ApplicationErrorCode.UserCurrentPasswordIncorrect"/> when the password is wrong
    /// or the account is locked by wrong passwords, <see cref="DomainErrorCode.TwoFactorLocked"/>
    /// while the second factor is locked, or
    /// <see cref="ApplicationErrorCode.TwoFactorCodeInvalid"/> for a wrong, expired or reused code.
    /// </returns>
    public async Task<Result<AuthenticatorSetup>> HandleAsync(
        BeginAuthenticatorSetupCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdAsync(currentUser.RequiredId(), ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (user.TwoFactorMethod is TwoFactorMethod.Authenticator)
        {
            return Error.Conflict(DomainErrorCode.AuthenticatorAlreadyEnabled);
        }

        if (!await passwordAttempts.VerifyReauthenticationAsync(user, command.CurrentPassword, ct))
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        return await accountLock.RunAsync<Result<AuthenticatorSetup>>(
            user,
            attempt => BeginLockedAsync(user, command.Code, attempt),
            Error.NotFound(ApplicationErrorCode.UserNotFound),
            ct
        );
    }

    private async Task<Result<AuthenticatorSetup>> BeginLockedAsync(
        User user,
        string code,
        CancellationToken ct
    )
    {
        if (user.TwoFactorMethod is TwoFactorMethod.Authenticator)
        {
            return Error.Conflict(DomainErrorCode.AuthenticatorAlreadyEnabled);
        }

        var now = clock.UtcNow;
        var verified = await secondFactor.VerifyLockedAsync(user, code, now, ct);
        if (verified.IsFailure)
        {
            return verified.Error!;
        }

        var secret = totp.GenerateSecret();
        var begun = user.BeginAuthenticatorSetup(
            protector.Protect(secret),
            now,
            options.SetupLifetime
        );
        if (begun.IsFailure)
        {
            return begun.Error!;
        }

        var account = user.Email?.Value ?? user.Id.ToString();
        return new AuthenticatorSetup(
            AuthenticatorKeys.Format(secret),
            AuthenticatorKeys.BuildUri(Organization.Name, account, secret)
        );
    }
}
