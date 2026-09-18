using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth.Commands;

/// <summary>
/// Carries the input required to start enrolling an authenticator application.
/// </summary>
/// <param name="UserId">Identifier of the signed-in user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record BeginAuthenticatorSetupCommand(Guid UserId, AuthenticatorSetupRequest Request)
    : ICommand<Result<AuthenticatorSetupResponse>>;

/// <summary>
/// Executes the command that creates a new shared secret. The user re-enters their password so a
/// stolen session cannot replace the second factor, and the secret only becomes active once
/// <see cref="ConfirmAuthenticatorCommandHandler"/> sees a code generated from it.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">Hasher used to verify the current password.</param>
/// <param name="totp">Generator of shared secrets.</param>
/// <param name="protector">Protector that encrypts the secret before it is stored.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class BeginAuthenticatorSetupCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    ITotpService totp,
    ISecretProtector protector,
    TwoFactorOptions options,
    ILogger<BeginAuthenticatorSetupCommandHandler> logger
) : ICommandHandler<BeginAuthenticatorSetupCommand, Result<AuthenticatorSetupResponse>>
{
    private const string Operation = "BeginAuthenticatorSetup";

    /// <summary>
    /// Handles the request to start the enrollment.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the enrollment data on success, or an application error on failure.</returns>
    public async Task<Result<AuthenticatorSetupResponse>> HandleAsync(
        BeginAuthenticatorSetupCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            string.IsNullOrEmpty(user.PasswordHash)
            || !hasher.Verify(command.Request.CurrentPassword, user.PasswordHash)
        )
        {
            logger.ReauthenticationRejected(user.Id, Operation);
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        var secret = totp.GenerateSecret();
        user.BeginAuthenticatorSetup(protector.Protect(secret), clock.UtcNow, options.SetupLifetime);
        await uow.SaveChangesAsync(ct);

        var account = user.Email ?? user.Id.ToString();
        return new AuthenticatorSetupResponse(
            AuthenticatorKeys.Format(secret),
            AuthenticatorKeys.BuildUri(options.Issuer, account, secret)
        );
    }
}
