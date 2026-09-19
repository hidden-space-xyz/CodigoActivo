using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to set admin.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="ActingUserId">Identifier of the acting user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record SetAdminCommand(Guid UserId, Guid ActingUserId, SetAdminRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command to set admin.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class SetAdminCommandHandler(
    IUserRepository users,
    PasswordAttemptGuard passwordAttempts,
    IClock clock,
    IUnitOfWork uow,
    AccountSecurityNotifier securityNotifier,
    ILogger<SetAdminCommandHandler> logger
) : ICommandHandler<SetAdminCommand, Result>
{
    private const string Operation = "SetAdmin";

    /// <summary>
    /// Handles the request to set admin. Granting the role first re-authenticates the acting
    /// administrator, so a hijacked session alone cannot escalate another account.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(SetAdminCommand command, CancellationToken ct = default)
    {
        var isAdmin = command.Request.IsAdmin;
        if (isAdmin && !await IsActingPasswordValidAsync(command, ct))
        {
            logger.ReauthenticationRejected(command.ActingUserId, Operation);
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (user.IsAdmin == isAdmin)
        {
            return Result.Success();
        }

        if (!isAdmin && await users.CountAsync(u => u.IsAdmin, ct) <= 1)
        {
            return Error.Forbidden(ErrorCode.UserCannotRemoveLastAdmin);
        }

        user.IsAdmin = isAdmin;
        user.UpdatedAt = clock.UtcNow;
        await uow.SaveChangesAsync(ct);
        logger.AdministratorFlagChanged(command.ActingUserId, user.Id, isAdmin);
        await securityNotifier.NotifyAsync(
            user,
            isAdmin ? AccountSecurityChange.AdminGranted : AccountSecurityChange.AdminRevoked,
            ct
        );
        return Result.Success();
    }

    private async Task<bool> IsActingPasswordValidAsync(
        SetAdminCommand command,
        CancellationToken ct
    )
    {
        var password = command.Request.CurrentPassword;
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var actingUser = await users.FindAsync(u => u.Id == command.ActingUserId, ct);
        return await passwordAttempts.VerifyReauthenticationAsync(actingUser, password, ct);
    }
}
