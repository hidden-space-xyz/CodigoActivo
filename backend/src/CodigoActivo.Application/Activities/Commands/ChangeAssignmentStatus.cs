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
/// Carries the input required to change assignment status.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record ChangeAssignmentStatusCommand(
    Guid ActivityId,
    Guid UserId,
    ChangeAssignmentStatusRequest Request
) : ICommand<Result>;

/// <summary>
/// Executes the command to change assignment status.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="readStore">Read side used to check the status catalog.</param>
/// <param name="executor">Executor of the read-side queries.</param>
/// <param name="notifier">The notifier value.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class ChangeAssignmentStatusCommandHandler(
    IActivityRepository activities,
    IReadStore readStore,
    IQueryExecutor executor,
    ActivitySignupNotifier notifier,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<ChangeAssignmentStatusCommand, Result>
{
    /// <summary>
    /// Handles the request to change assignment status.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        ChangeAssignmentStatusCommand command,
        CancellationToken ct = default
    )
    {
        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        var assignment = activity?.AssignmentOf(command.UserId);
        if (activity is null || assignment is null)
        {
            return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);
        }

        var statusId = command.Request.AssignmentStatusId;
        if (
            !await executor.AnyAsync(
                readStore.AssignmentStatusTypes.Where(type => type.Id == statusId),
                ct
            )
        )
        {
            return Error.NotFound(ErrorCode.AssignmentStatusTypeNotFound);
        }

        var previousStatusId = activity.ChangeAssignmentStatus(command.UserId, statusId);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);

        if (previousStatusId != statusId && AssignmentDecisions.IsDecision(statusId))
        {
            await notifier.NotifyDecisionAsync(
                command.ActivityId,
                command.UserId,
                statusId,
                assignment.ActivityRoleTypeId,
                ct
            );
        }

        return Result.Success();
    }
}
