using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to change the role of a person signed up to an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the person.</param>
/// <param name="Role">New role.</param>
public sealed record ChangeAssignmentRoleCommand(
    ActivityId ActivityId,
    UserId UserId,
    ActivityRole Role
) : ICommand<Result>;

/// <summary>
/// Executes the command to change the role of a person signed up to an activity, keeping the status
/// and date of the signup. The type of the person is not checked against the role.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
public sealed class ChangeAssignmentRoleCommandHandler(IActivityRepository activities)
    : ICommandHandler<ChangeAssignmentRoleCommand, Result>
{
    /// <summary>
    /// Handles the request to change the role of a person signed up to an activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, also when the role does not change, or
    /// <see cref="DomainErrorCode.ActivityAssignmentNotFound"/> when the activity or the signup is
    /// missing.
    /// </returns>
    public async Task<Result> HandleAsync(
        ChangeAssignmentRoleCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(DomainErrorCode.ActivityAssignmentNotFound);
        }

        return activity.ChangeAssignmentRole(command.UserId, command.Role);
    }
}
