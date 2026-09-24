using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to email the code that confirms deleting an account.
/// </summary>
/// <param name="UserId">Identifier of the signed-in user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record RequestAccountDeletionCodeCommand(
    Guid UserId,
    AccountDeletionCodeRequest Request
) : ICommand<Result>;

/// <summary>
/// Executes the command that emails the one-time code confirming an account deletion. The
/// password is demanded first so a stolen session cannot start the flow, and the code reuses the
/// storage, lifetime, cooldown and lockout of the emailed login code. Users whose second factor is
/// an authenticator application read their code from it and never reach this command.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">Hasher used to verify the current password.</param>
/// <param name="options">Second-factor configuration.</param>
/// <param name="loginCodes">Issuer of emailed one-time codes.</param>
public sealed class RequestAccountDeletionCodeCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    TwoFactorOptions options,
    LoginCodeIssuer loginCodes
) : ICommandHandler<RequestAccountDeletionCodeCommand, Result>
{
    /// <summary>
    /// Handles the request to email the account deletion code.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        RequestAccountDeletionCodeCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (user.IsAdmin && await users.CountAsync(u => u.IsAdmin, ct) <= 1)
        {
            return Error.Forbidden(ErrorCode.UserDeleteLastAdminForbidden);
        }

        if (
            string.IsNullOrEmpty(user.PasswordHash)
            || !hasher.Verify(command.Request.CurrentPassword, user.PasswordHash)
        )
        {
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        if (user.TwoFactorMethod != TwoFactorMethod.Email)
        {
            return Error.Conflict(ErrorCode.TwoFactorResendNotAllowed);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ErrorCode.TwoFactorLocked);
        }

        if (now < user.LoginCodeLastSentAt + options.ResendCooldown)
        {
            return Error.Conflict(ErrorCode.TwoFactorResendCooldownActive);
        }

        var issued = await loginCodes.IssueAccountDeletionAsync(user, now, ct);
        if (issued.IsFailure)
        {
            return issued;
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
