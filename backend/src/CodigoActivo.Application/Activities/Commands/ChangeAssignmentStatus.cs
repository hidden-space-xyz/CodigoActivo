using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to change the status of a person signed up to an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the person.</param>
/// <param name="AssignmentStatusId">Catalog identifier of the new status.</param>
public sealed record ChangeAssignmentStatusCommand(
    ActivityId ActivityId,
    UserId UserId,
    Guid AssignmentStatusId
) : ICommand<Result>;

/// <summary>
/// Executes the command to change the status of a person signed up to an activity. The person
/// is told about a decision once the change is committed.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
public sealed class ChangeAssignmentStatusCommandHandler(IActivityRepository activities)
    : ICommandHandler<ChangeAssignmentStatusCommand, Result>
{
    /// <summary>
    /// Handles the request to change the status of a person signed up to an activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        ChangeAssignmentStatusCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity?.AssignmentOf(command.UserId) is null)
        {
            return Error.NotFound(DomainErrorCode.ActivityAssignmentNotFound);
        }

        if (!CatalogIds.AssignmentStatuses.TryGetValue(command.AssignmentStatusId, out var status))
        {
            return Error.NotFound(ApplicationErrorCode.AssignmentStatusTypeNotFound);
        }

        activity.ChangeAssignmentStatus(command.UserId, status);
        return Result.Success();
    }
}
