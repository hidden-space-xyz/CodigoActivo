using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Commands;

public sealed record SetAdminCommand(Guid UserId, bool IsAdmin) : ICommand<Result>;

public sealed class SetAdminCommandHandler(IUserRepository users, IClock clock, IUnitOfWork uow)
    : ICommandHandler<SetAdminCommand, Result>
{
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
