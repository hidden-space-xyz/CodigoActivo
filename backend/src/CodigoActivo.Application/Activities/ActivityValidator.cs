using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities;

public sealed class ActivityValidator(
    IEventRepository events,
    IFileRepository files,
    IActivityModalityTypeRepository modalityTypes,
    IActivityRoleTypeRepository roleTypes,
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
        var eventDates = await GetEventDatesAsync(eventId, ct);
        if (eventDates is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var schedule = ValidateActivitySchedule(eventDates, startsAt, endsAt);
        if (schedule.IsFailure)
        {
            return schedule.Error!;
        }

        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.ActivityThumbnailNotFound);
        }

        if (!await modalityTypes.ExistsAsync(m => m.Id == modalityTypeId, ct))
        {
            return Error.BadRequest(ErrorCode.ActivityModalityTypeNotFound);
        }

        var capacities = await ValidateRoleCapacitiesAsync(roleCapacities, ct);
        if (capacities.Error is { } capacityError)
        {
            return capacityError;
        }

        return new ValidatedActivity(schedule.Value, capacities.Value);
    }

    internal static void SyncRoleCapacities(Activity activity, List<RoleCapacityItem> desired)
    {
        var desiredByRole = desired.ToDictionary(
            item => item.RoleTypeId,
            item => item.DesiredCount
        );

        var removed = activity.RoleCapacities.Where(capacity =>
            !desiredByRole.ContainsKey(capacity.ActivityRoleTypeId)
        );
        foreach (var existing in removed.ToList())
        {
            activity.RoleCapacities.Remove(existing);
        }

        foreach (var (roleTypeId, desiredCount) in desiredByRole)
        {
            var existing = activity.RoleCapacities.FirstOrDefault(capacity =>
                capacity.ActivityRoleTypeId == roleTypeId
            );
            if (existing is null)
            {
                activity.RoleCapacities.Add(
                    new ActivityRoleCapacity
                    {
                        ActivityId = activity.Id,
                        ActivityRoleTypeId = roleTypeId,
                        DesiredCount = desiredCount,
                    }
                );
            }
            else
            {
                existing.DesiredCount = desiredCount;
            }
        }
    }

    private async Task<Result<List<RoleCapacityItem>>> ValidateRoleCapacitiesAsync(
        IReadOnlyList<ActivityRoleCapacityRequest>? requests,
        CancellationToken ct
    )
    {
        if (requests is null || requests.Count is 0)
        {
            return new List<RoleCapacityItem>();
        }

        if (requests.Select(item => item.ActivityRoleTypeId).ToHashSet().Count != requests.Count)
        {
            return Error.BadRequest(ErrorCode.ActivityRoleCapacityDuplicated);
        }

        var roleIds = requests.Select(item => item.ActivityRoleTypeId).ToList();
        var knownCount = await roleTypes.CountAsync(role => roleIds.Contains(role.Id), ct);
        return knownCount != roleIds.Count
            ? (Result<List<RoleCapacityItem>>)Error.BadRequest(ErrorCode.ActivityRoleTypeNotFound)
            : (Result<List<RoleCapacityItem>>)
                requests
                    .Select(item => new RoleCapacityItem(
                        item.ActivityRoleTypeId,
                        item.DesiredCount!.Value
                    ))
                    .ToList();
    }

    private Task<EventDates?> GetEventDatesAsync(Guid eventId, CancellationToken ct)
    {
        return executor.FirstOrDefaultAsync(
            events
                .Query()
                .Where(e => e.Id == eventId)
                .Select(e => new EventDates(e.EventStartsAt, e.EventEndsAt)),
            ct
        );
    }

    private Result<ActivitySchedule> ValidateActivitySchedule(
        EventDates eventDates,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt
    )
    {
        if (startsAt is not { } start || endsAt is not { } end)
        {
            return Error.BadRequest(ErrorCode.ActivityScheduleRequired);
        }

        if (end <= start)
        {
            return Error.BadRequest(ErrorCode.ActivityScheduleInvalidRange);
        }

        var startDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(start, clock.TimeZone).DateTime
        );
        var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end, clock.TimeZone).DateTime);
        return startDate < eventDates.StartsAt || endDate > eventDates.EndsAt
            ? (Result<ActivitySchedule>)
                Error.BadRequest(ErrorCode.ActivityScheduleOutsideEventRange)
            : (Result<ActivitySchedule>)
                new ActivitySchedule(start.ToUniversalTime(), end.ToUniversalTime());
    }

    internal readonly record struct ActivitySchedule(
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt
    );

    internal readonly record struct ValidatedActivity(
        ActivitySchedule Schedule,
        List<RoleCapacityItem> Capacities
    );

    internal readonly record struct RoleCapacityItem(Guid RoleTypeId, int DesiredCount);

    private sealed record EventDates(DateOnly StartsAt, DateOnly EndsAt);
}
