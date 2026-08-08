using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Commands;

public sealed record ChangeUserTypeCommand(Guid UserId, Guid UserTypeId)
    : ICommand<Result<UserResponse>>;

public sealed class ChangeUserTypeCommandHandler(
    IUserRepository users,
    IUserTypeRepository userTypes,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetUserByIdQueryHandler getById
) : ICommandHandler<ChangeUserTypeCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> HandleAsync(
        ChangeUserTypeCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (!await userTypes.ExistsAsync(ut => ut.Id == command.UserTypeId, ct))
        {
            return Error.NotFound(ErrorCode.UserTypeNotFound);
        }

        if (user.UserTypeId != command.UserTypeId)
        {
            user.UserTypeId = command.UserTypeId;
            user.UpdatedAt = clock.UtcNow;
            await uow.SaveChangesAsync(ct);
            await cacheInvalidator.InvalidateAsync(CacheTags.Users);
        }

        return await getById.HandleAsync(new GetUserByIdQuery(command.UserId), ct);
    }
}
