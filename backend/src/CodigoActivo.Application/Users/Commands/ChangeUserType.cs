using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to change user type.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="UserTypeId">Identifier of the user type.</param>
public sealed record ChangeUserTypeCommand(Guid UserId, Guid UserTypeId)
    : ICommand<Result<UserResponse>>;

/// <summary>
/// Executes the command to change user type.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="userTypes">Repository used to persist and retrieve user types.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="getById">Handler used to retrieve user by identifier.</param>
public sealed class ChangeUserTypeCommandHandler(
    IUserRepository users,
    IUserTypeRepository userTypes,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetUserByIdQueryHandler getById
) : ICommandHandler<ChangeUserTypeCommand, Result<UserResponse>>
{
    /// <summary>
    /// Handles the request to change user type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a user on success, or an application error on failure.</returns>
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
