using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to change user type.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="UserTypeId">Identifier of the user type.</param>
public sealed record ChangeUserTypeCommand(Guid UserId, Guid UserTypeId) : ICommand<Result>;

/// <summary>
/// Executes the command to change user type.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="readStore">Read side used to check the user type catalog.</param>
/// <param name="executor">Executor of the read-side queries.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class ChangeUserTypeCommandHandler(
    IUserRepository users,
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<ChangeUserTypeCommand, Result>
{
    /// <summary>
    /// Handles the request to change user type.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        ChangeUserTypeCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            !await executor.AnyAsync(
                readStore.UserTypes.Where(type => type.Id == command.UserTypeId),
                ct
            )
        )
        {
            return Error.NotFound(ErrorCode.UserTypeNotFound);
        }

        if (user.UserTypeId != command.UserTypeId)
        {
            user.ChangeType(command.UserTypeId, clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            await cacheInvalidator.InvalidateAsync(CacheTags.Users);
        }

        return Result.Success();
    }
}
