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
/// Carries the input required to start enrolling an authenticator application.
/// </summary>
/// <param name="CurrentPassword">Password of the signed-in user, re-entered to authorize the change.</param>
public sealed record BeginAuthenticatorSetupCommand(
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword
) : ICommand<Result<AuthenticatorSetup>>;

/// <summary>
/// Executes the command that creates a new shared secret. The user re-enters their password so a
/// stolen session cannot replace the second factor, and the secret only becomes active once
/// <see cref="ConfirmAuthenticatorCommandHandler"/> sees a code generated from it. An account that
/// already uses an authenticator is refused: it returns to email first, which takes a current code.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="totp">Generator of shared secrets.</param>
/// <param name="protector">Protector that encrypts the secret before it is stored.</param>
/// <param name="options">Second-factor configuration.</param>
public sealed class BeginAuthenticatorSetupCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
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
    /// <returns>A task whose result contains the enrollment data on success, or an application error on failure.</returns>
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

        if (!await passwordAttempts.VerifyReauthenticationAsync(user, command.CurrentPassword, ct))
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        var secret = totp.GenerateSecret();
        var begun = user.BeginAuthenticatorSetup(
            protector.Protect(secret),
            clock.UtcNow,
            options.SetupLifetime
        );
        if (begun.IsFailure)
        {
            return begun.Error!;
        }

        var account = user.Email?.Value ?? user.Id.ToString();
        return new AuthenticatorSetup(
            AuthenticatorKeys.Format(secret),
            AuthenticatorKeys.BuildUri(options.Issuer, account, secret)
        );
    }
}
