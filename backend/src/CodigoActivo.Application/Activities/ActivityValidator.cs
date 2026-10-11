using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Validates activity input before it is processed: the rules of the schedule and the role
/// capacities belong to the domain, and this collaborator adds the checks that need other
/// aggregates (the event and the thumbnail).
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ActivityValidator(
    IEventRepository events,
    IStoredFileRepository files,
    IClock clock
)
{
    internal async Task<Result<ValidatedActivity>> ValidateActivityAsync(
        EventId eventId,
        ActivityDraft draft,
        CancellationToken ct
    )
    {
        var ev = await events.GetByIdAsync(eventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var schedule = ActivitySchedule.Create(
            draft.ActivityStartsAt,
            draft.ActivityEndsAt,
            ev.Calendar,
            clock.TimeZone
        );
        if (schedule.IsFailure)
        {
            return schedule.Error!;
        }

        if (!await files.ExistsAsync(draft.ThumbnailId, ct))
        {
            return Error.Validation(ApplicationErrorCode.ActivityThumbnailNotFound);
        }

        var capacities = RoleCapacityPlan.Create([
            .. draft.RoleCapacities.Select(item => new RoleCapacity(item.Role, item.DesiredCount)),
        ]);
        if (capacities.IsFailure)
        {
            return capacities.Error!;
        }

        return new ValidatedActivity(schedule.Value, draft.Modality, capacities.Value);
    }

    internal readonly record struct ValidatedActivity(
        ActivitySchedule Schedule,
        ActivityModality Modality,
        RoleCapacityPlan Capacities
    );
}
