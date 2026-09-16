using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to set admin.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="IsAdmin">Whether admin.</param>
public sealed record SetAdminCommand(Guid UserId, bool IsAdmin) : ICommand<Result>;

/// <summary>
/// Executes the command to set admin.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class SetAdminCommandHandler(IUserRepository users, IClock clock, IUnitOfWork uow)
    : ICommandHandler<SetAdminCommand, Result>
{
    /// <summary>
    /// Handles the request to set admin.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(SetAdminCommand command, CancellationToken ct = default)
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (user.IsAdmin == command.IsAdmin)
        {
            return Result.Success();
        }

        if (!command.IsAdmin && await users.CountAsync(u => u.IsAdmin, ct) <= 1)
        {
            return Error.Forbidden(ErrorCode.UserCannotRemoveLastAdmin);
        }

        user.IsAdmin = command.IsAdmin;
        user.UpdatedAt = clock.UtcNow;
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
