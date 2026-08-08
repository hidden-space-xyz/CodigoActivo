using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Events;

public static class EventRules
{
    internal static Result<EventSchedule> ValidateSchedule(
        DateOnly? eventStartsAt,
        DateOnly? eventEndsAt,
        DateTimeOffset? earlySignupStartsAt,
        DateTimeOffset? signupStartsAt,
        DateTimeOffset? signupEndsAt
    )
    {
        if (
            eventStartsAt is not { } eventStart
            || eventEndsAt is not { } eventEnd
            || signupStartsAt is not { } signupStart
            || signupEndsAt is not { } signupEnd
        )
        {
            return Error.BadRequest(ErrorCode.EventScheduleRequired);
        }

        if (eventEnd < eventStart || signupEnd <= signupStart)
        {
            return Error.BadRequest(ErrorCode.EventScheduleInvalidRange);
        }

        if (earlySignupStartsAt is { } earlyStart && earlyStart >= signupStart)
        {
            return Error.BadRequest(ErrorCode.EventEarlySignupNotBeforeSignup);
        }

        if (DateOnly.FromDateTime(signupStart.UtcDateTime) > eventEnd)
        {
            return Error.BadRequest(ErrorCode.EventScheduleInvalidRange);
        }

        return new EventSchedule(
            eventStart,
            eventEnd,
            earlySignupStartsAt?.ToUniversalTime(),
            signupStart.ToUniversalTime(),
            signupEnd.ToUniversalTime()
        );
    }

    public static void SyncCategories(Event ev, IReadOnlyList<Guid> categoryTypeIds)
    {
        var desired = categoryTypeIds.Distinct().ToHashSet();

        var removed = ev.Categories.Where(c => !desired.Contains(c.EventCategoryTypeId)).ToList();
        foreach (var existing in removed)
        {
            ev.Categories.Remove(existing);
        }

        var current = ev.Categories.Select(c => c.EventCategoryTypeId).ToHashSet();
        foreach (var categoryTypeId in desired.Except(current))
        {
            ev.Categories.Add(
                new EventCategory { EventId = ev.Id, EventCategoryTypeId = categoryTypeId }
            );
        }
    }

    internal readonly record struct EventSchedule(
        DateOnly EventStartsAt,
        DateOnly EventEndsAt,
        DateTimeOffset? EarlySignupStartsAt,
        DateTimeOffset SignupStartsAt,
        DateTimeOffset SignupEndsAt
    );
}
