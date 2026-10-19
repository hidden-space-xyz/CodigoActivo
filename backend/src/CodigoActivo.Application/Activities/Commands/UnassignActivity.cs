using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to withdraw a person from an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the person withdrawn.</param>
public sealed record UnassignActivityCommand(ActivityId ActivityId, UserId UserId)
    : ICommand<Result>;

/// <summary>
/// Executes the command to withdraw a person from an activity. The signed-in user may withdraw
/// themselves or one of their dependents while the signup is open; an administrator may withdraw
/// anyone at any time. The cancelled signup is kept in a blocked copy.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="actingUser">Policy that decides for whom the signed-in user may act.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="signupGate">Gate that checks the signup window.</param>
/// <param name="eraser">Service that commits the cancellation with the blocked copy.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class UnassignActivityCommandHandler(
    IActivityRepository activities,
    ActingUserPolicy actingUser,
    ICurrentUser currentUser,
    SignupGate signupGate,
    ParticipationEraser eraser,
    IClock clock
) : ICommandHandler<UnassignActivityCommand, Result>
{
    /// <summary>
    /// Handles the request to withdraw a person from an activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        UnassignActivityCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var allowed = await actingUser.EnsureMayActForAsync(command.UserId, ct);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity?.AssignmentOf(command.UserId) is null)
        {
            return Error.NotFound(DomainErrorCode.ActivityAssignmentNotFound);
        }

        if (!currentUser.IsAdmin)
        {
            var signup = await signupGate.EnsureSignupOpenAsync(
                activity,
                [command.UserId],
                isAdmin: false,
                ct
            );
            if (signup.IsFailure)
            {
                return signup.Error!;
            }
        }

        var unassigned = activity.Unassign(command.UserId);
        if (unassigned.IsFailure)
        {
            return unassigned;
        }

        await eraser.EraseAsync(
            ParticipationDeletion.OfSignup(
                activity,
                command.UserId,
                currentUser.RequiredId(),
                clock.UtcNow
            ),
            ct
        );
        return unassigned;
    }
}
