using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Users.Commands;

public sealed record ChangePasswordCommand(Guid UserId, ChangePasswordRequest Request)
    : ICommand<Result>;

public sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return Error.BadRequest(ErrorCode.UserPasswordNotSet);
        }

        if (!hasher.Verify(command.Request.CurrentPassword, user.PasswordHash))
        {
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        user.PasswordHash = hasher.Hash(command.Request.NewPassword);
        user.UpdatedAt = clock.UtcNow;
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
