using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Validates activity input before it is processed: the rules of the schedule and the role
/// capacities belong to the domain, and this collaborator adds the checks that need other
/// aggregates (the event, the thumbnail and the catalogs).
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
            ev.Calendar.Start,
            ev.Calendar.End,
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

        if (
            !CatalogIds.ActivityModalities.TryGetValue(
                draft.ActivityModalityTypeId,
                out var modality
            )
        )
        {
            return Error.Validation(ApplicationErrorCode.ActivityModalityTypeNotFound);
        }

        var requested = draft.RoleCapacities;
        var roles = new List<RoleCapacity>(requested.Count);
        foreach (var item in requested)
        {
            if (!CatalogIds.ActivityRoles.TryGetValue(item.ActivityRoleTypeId, out var role))
            {
                return
                    requested.Select(capacity => capacity.ActivityRoleTypeId).Distinct().Count()
                    != requested.Count
                    ? Error.Validation(DomainErrorCode.ActivityRoleCapacityDuplicated)
                    : Error.Validation(ApplicationErrorCode.ActivityRoleTypeNotFound);
            }

            roles.Add(new RoleCapacity(role, item.DesiredCount));
        }

        var capacities = RoleCapacityPlan.Create(roles);
        if (capacities.IsFailure)
        {
            return capacities.Error!;
        }

        return new ValidatedActivity(schedule.Value, modality, capacities.Value);
    }

    internal readonly record struct ValidatedActivity(
        ActivitySchedule Schedule,
        ActivityModality Modality,
        RoleCapacityPlan Capacities
    );
}
