using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to email a new login code for an open challenge.
/// </summary>
/// <param name="UserId">Identifier of the user whose password was already accepted.</param>
public sealed record ResendTwoFactorCodeCommand(UserId UserId) : ICommand<Result>;

/// <summary>
/// Executes the command to email a new login code, replacing the previous one.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="loginCodes">Issuer of emailed login codes.</param>
public sealed class ResendTwoFactorCodeCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    TwoFactorOptions options,
    LoginCodeIssuer loginCodes
) : ICommandHandler<ResendTwoFactorCodeCommand, Result>
{
    /// <summary>
    /// Handles the request to resend the login code.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        ResendTwoFactorCodeCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (user.TwoFactorMethod != TwoFactorMethod.Email)
        {
            return Error.Conflict(ApplicationErrorCode.TwoFactorResendNotAllowed);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ApplicationErrorCode.TwoFactorLocked);
        }

        if (user.IsLoginCodeResendCoolingDown(now, options.ResendCooldown))
        {
            return Error.Conflict(ApplicationErrorCode.TwoFactorResendCooldownActive);
        }

        var issued = await loginCodes.IssueAsync(user, now, ct);
        if (issued.IsFailure)
        {
            return issued;
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
