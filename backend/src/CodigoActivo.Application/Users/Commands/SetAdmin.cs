using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to grant or revoke the administrator rights of a user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="IsAdmin">Whether the user is an administrator from now on.</param>
/// <param name="CurrentPassword">
/// Password of the signed-in administrator. Required to grant the rights; ignored when revoking them.
/// </param>
public sealed record SetAdminCommand(
    UserId UserId,
    bool IsAdmin,
    [property: MaxLength(128)] string? CurrentPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command to grant or revoke the administrator rights of a user. Nobody changes
/// their own rights, administrators included, a dependent never gets them and the initial
/// administrator never loses them.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class SetAdminCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    PasswordAttemptGuard passwordAttempts,
    IClock clock
) : ICommandHandler<SetAdminCommand, Result>
{
    /// <summary>
    /// Handles the request to grant or revoke the administrator rights of a user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(SetAdminCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.UserId == currentUser.RequiredId())
        {
            return Error.Forbidden(ApplicationErrorCode.UserSelfAdminChangeForbidden);
        }

        if (
            command.IsAdmin
            && !await passwordAttempts.VerifyActingUserAsync(command.CurrentPassword, ct)
        )
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        return user.SetAdministrator(command.IsAdmin, clock.UtcNow);
    }
}
