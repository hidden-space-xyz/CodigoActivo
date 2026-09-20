using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Auth.Commands;

/// <summary>
/// Carries the input required to confirm an authenticator enrollment.
/// </summary>
/// <param name="UserId">Identifier of the signed-in user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record ConfirmAuthenticatorCommand(Guid UserId, ConfirmAuthenticatorRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command that activates the pending authenticator once the user proves the
/// application was set up correctly. From then on logins require its codes instead of email.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
public sealed class ConfirmAuthenticatorCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    AuthenticatorCodeVerifier authenticatorCodes,
    AccountSecurityNotifier securityNotifier
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
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        var now = clock.UtcNow;
        if (!user.HasPendingAuthenticator(now))
        {
            return Error.BadRequest(ErrorCode.AuthenticatorSetupExpired);
        }

        var step = authenticatorCodes.Match(
            user.PendingAuthenticatorKey,
            command.Request.Code,
            lastUsedStep: null
        );
        if (step is null)
        {
            return Error.BadRequest(ErrorCode.TwoFactorCodeInvalid);
        }

        user.EnableAuthenticator(step.Value, now);
        await uow.SaveChangesAsync(ct);
        await securityNotifier.NotifyAsync(user, AccountSecurityChange.AuthenticatorEnabled, ct);
        return Result.Success();
    }
}
