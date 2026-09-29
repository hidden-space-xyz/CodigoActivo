using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to unassign activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="IsAdmin">Whether admin.</param>
public sealed record UnassignActivityCommand(Guid ActivityId, Guid UserId, bool IsAdmin)
    : ICommand<Result>;

/// <summary>
/// Executes the command to unassign activity.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="signupGate">The signup gate value.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class UnassignActivityCommandHandler(
    IActivityRepository activities,
    SignupGate signupGate,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UnassignActivityCommand, Result>
{
    /// <summary>
    /// Handles the request to unassign activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        UnassignActivityCommand command,
        CancellationToken ct = default
    )
    {
        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity?.AssignmentOf(command.UserId) is null)
        {
            return Error.NotFound(ErrorCode.ActivityAssignmentNotFound);
        }

        if (!command.IsAdmin)
        {
            var signup = await signupGate.EnsureSignupOpenAsync(
                activity,
                [command.UserId],
                command.IsAdmin,
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
            return unassigned.Error!;
        }

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Activities);
        return Result.Success();
    }
}
