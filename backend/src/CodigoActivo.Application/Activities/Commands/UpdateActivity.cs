using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Carries the input required to update the activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Activity">New details, schedule and capacities of the activity.</param>
public sealed record UpdateActivityCommand(ActivityId ActivityId, ActivityDraft Activity)
    : ICommand<Result>;

/// <summary>
/// Executes the command to update the activity, keeping its signups. A replaced thumbnail is
/// removed after the commit when nothing else uses it.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="validator">Validator of the event, schedule, thumbnail and role capacities.</param>
/// <param name="currentUser">Person the use case runs for, recorded as the editor.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work whose transaction validates and saves the activity.</param>
public sealed class UpdateActivityCommandHandler(
    IActivityRepository activities,
    ActivityValidator validator,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<UpdateActivityCommand, Result>
{
    /// <summary>
    /// Handles the request to update the activity.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success; <see cref="ApplicationErrorCode.ActivityNotFound"/>,
    /// <see cref="ApplicationErrorCode.EventNotFound"/> or
    /// <see cref="ApplicationErrorCode.ActivityThumbnailNotFound"/> when one is missing, or the
    /// domain error of an invalid schedule or role capacity plan.
    /// </returns>
    public async Task<Result> HandleAsync(
        UpdateActivityCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        var draft = command.Activity;

        var activity = await activities.GetByIdAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound(ApplicationErrorCode.ActivityNotFound);
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => UpdateLockedAsync(activity, draft, attempt),
            ct
        );
    }

    private async Task<Result> UpdateLockedAsync(
        Activity activity,
        ActivityDraft draft,
        CancellationToken ct
    )
    {
        var validated = await validator.ValidateActivityAsync(activity.EventId, draft, ct);
        if (validated.IsFailure)
        {
            return validated.Error!;
        }

        activity.Update(
            draft.ToDetails(),
            validated.Value.Schedule,
            validated.Value.Capacities,
            currentUser.RequiredId(),
            clock.UtcNow
        );
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
