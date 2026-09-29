using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to change assignment role.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record ChangeAssignmentRoleCommand(
    Guid ActivityId,
    Guid UserId,
    ChangeAssignmentRoleRequest Request
) : ICommand<Result>;

/// <summary>
/// Executes the command to change assignment role.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="readStore">Read side used to check the role catalog.</param>
/// <param name="executor">Executor of the read-side queries.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class ChangeAssignmentRoleCommandHandler(
    IActivityRepository activities,
    IReadStore readStore,
    IQueryExecutor executor,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<ChangeAssignmentRoleCommand, Result>
{
    /// <summary>
    /// Handles the request to change assignment role.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        ChangeAssignmentRoleCommand command,
        CancellationToken ct = default
    )
    {
        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity?.AssignmentOf(command.UserId) is null)
        {
            return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);
        }

        var roleTypeId = command.Request.ActivityRoleTypeId;
        if (
            !await executor.AnyAsync(
                readStore.ActivityRoleTypes.Where(type => type.Id == roleTypeId),
                ct
            )
        )
        {
            return Error.NotFound(ErrorCode.ActivityRoleTypeNotFound);
        }

        if (activity.ChangeAssignmentRole(command.UserId, roleTypeId))
        {
            await uow.SaveChangesAsync(ct);
            await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
        }

        return Result.Success();
    }
}
