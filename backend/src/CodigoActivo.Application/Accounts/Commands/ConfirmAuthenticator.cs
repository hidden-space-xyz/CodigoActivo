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
/// Carries the input required to confirm an authenticator enrollment.
/// </summary>
/// <param name="Code">Code the authenticator shows.</param>
public sealed record ConfirmAuthenticatorCommand(
    [property: Required, MaxLength(16), NotBlank] string Code
) : ICommand<Result>;

/// <summary>
/// Executes the command that activates the pending authenticator once the user proves the
/// application was set up correctly. From then on logins require its codes instead of email.
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
        if (!user.HasPendingAuthenticator(now))
        {
            return Error.Validation(ApplicationErrorCode.AuthenticatorSetupExpired);
        }

        var step = authenticatorCodes.Match(user.PendingAuthenticatorKey, command.Code);
        if (step is null)
        {
            return Error.Validation(ApplicationErrorCode.TwoFactorCodeInvalid);
        }

        var enabled = user.EnableAuthenticator(step.Value, now);
        if (enabled.IsFailure)
        {
            return enabled;
        }

        return Result.Success();
    }
}
