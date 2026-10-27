using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to confirm an authenticator enrollment.
/// </summary>
/// <param name="Code">Code the authenticator shows.</param>
public sealed record ConfirmAuthenticatorCommand(
    [property: Required, MaxLength(16), NotBlank] string Code
) : ICommand<Result>;

/// <summary>
/// Executes the command that activates the pending authenticator of the signed-in user once they
/// prove the application was set up correctly. From then on logins require its codes instead of
/// email, and the holder is emailed a security alert once the change is committed. A wrong code is
/// answered with <see cref="ApplicationErrorCode.TwoFactorCodeInvalid"/> without being counted; an
/// enrollment that expired or was never started with
/// <see cref="DomainErrorCode.AuthenticatorSetupExpired"/>, and an account that already uses an
/// authenticator with <see cref="DomainErrorCode.AuthenticatorAlreadyEnabled"/>.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
public sealed class ConfirmAuthenticatorCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    IClock clock,
    AuthenticatorCodeVerifier authenticatorCodes
) : ICommandHandler<ConfirmAuthenticatorCommand, Result>
{
    /// <summary>
    /// Handles the request to confirm the enrollment.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ConfirmAuthenticatorCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdAsync(currentUser.RequiredId(), ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var now = clock.UtcNow;
        if (!user.Authenticator.HasPendingEnrollment(now))
        {
            return Error.Validation(DomainErrorCode.AuthenticatorSetupExpired);
        }

        var step = authenticatorCodes.Match(user.Authenticator.PendingKey, command.Code);
        if (step is null)
        {
            return Error.Validation(ApplicationErrorCode.TwoFactorCodeInvalid);
        }

        return user.EnableAuthenticator(step.Value, now);
    }
}
