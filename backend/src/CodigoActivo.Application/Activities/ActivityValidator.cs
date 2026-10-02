using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities.Contracts;
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
/// <param name="readStore">Read side used to check the modality and role catalogs.</param>
/// <param name="executor">Executor of the read-side queries.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ActivityValidator(
    IEventRepository events,
    IStoredFileRepository files,
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock
)
{
    internal async Task<Result<ValidatedActivity>> ValidateActivityAsync(
        Guid eventId,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        Guid thumbnailId,
        Guid modalityTypeId,
        IReadOnlyList<ActivityRoleCapacityRequest>? roleCapacities,
        CancellationToken ct
    )
    {
        var ev = await events.GetByIdAsync(eventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var schedule = ActivitySchedule.Create(
            startsAt,
            endsAt,
            ev.EventStartsAt,
            ev.EventEndsAt,
            clock.TimeZone
        );
        if (schedule.IsFailure)
        {
            return schedule.Error!;
        }

        if (!await files.ExistsAsync(thumbnailId, ct))
        {
            return Error.Validation(ErrorCode.ActivityThumbnailNotFound);
        }

        if (
            !await executor.AnyAsync(
                readStore.ActivityModalityTypes.Where(type => type.Id == modalityTypeId),
                ct
            )
        )
        {
            return Error.Validation(ErrorCode.ActivityModalityTypeNotFound);
        }

        var capacities = RoleCapacityPlan.Create(
            roleCapacities
                ?.Select(item => new RoleCapacity(
                    item.ActivityRoleTypeId,
                    item.DesiredCount!.Value
                ))
                .ToList()
        );
        if (capacities.IsFailure)
        {
            return capacities.Error!;
        }

        var roleIds = capacities.Value.Items.Select(item => item.ActivityRoleTypeId).ToList();
        if (
            roleIds.Count > 0
            && (
                await executor.ToListAsync(
                    readStore
                        .ActivityRoleTypes.Where(type => roleIds.Contains(type.Id))
                        .Select(type => type.Id),
                    ct
                )
            ).Count != roleIds.Count
        )
        {
            return Error.Validation(ErrorCode.ActivityRoleTypeNotFound);
        }

        return new ValidatedActivity(schedule.Value, capacities.Value);
    }

    internal readonly record struct ValidatedActivity(
        ActivitySchedule Schedule,
        RoleCapacityPlan Capacities
    );
}
