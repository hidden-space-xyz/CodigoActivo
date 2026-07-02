using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class EventService(
    IEventRepository events,
    IActivityRepository activities,
    IFileRepository files,
    IUnitOfWork uow
) : IEventService
{
    public IQueryable<EventResponse> Query()
    {
        return events.Query().Select(Projections.Event);
    }

    public async Task<Result<EventResponse>> CreateAsync(
        CreateEventRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var schedule = ValidateSchedule(
            request.EventStartsAt,
            request.EventEndsAt,
            request.SignupStartsAt,
            request.SignupEndsAt
        );
        if (schedule.IsFailure)
        {
            return schedule.Error!;
        }

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        var ev = new Event
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = request.Description,
            EventStartsAt = schedule.Value.EventStartsAt,
            EventEndsAt = schedule.Value.EventEndsAt,
            SignupStartsAt = schedule.Value.SignupStartsAt,
            SignupEndsAt = schedule.Value.SignupEndsAt,
            ThumbnailId = request.ThumbnailId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId,
        };

        await events.AddAsync(ev, ct);
        await uow.SaveChangesAsync(ct);

        return ev.ToResponse();
    }

    public async Task<Result<EventResponse>> UpdateAsync(
        Guid id,
        UpdateEventRequest request,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var schedule = ValidateSchedule(
            request.EventStartsAt,
            request.EventEndsAt,
            request.SignupStartsAt,
            request.SignupEndsAt
        );
        if (schedule.IsFailure)
        {
            return schedule.Error!;
        }

        var ev = await events.FindAsync(e => e.Id == id, ct);
        if (ev is null)
        {
            return Error.NotFound();
        }

        // An activity may never fall outside its event's days. Reject a date change that
        // would leave an existing activity orphaned (dates compared, hours ignored).
        var (lowerInclusive, upperExclusive) = DayBounds(
            schedule.Value.EventStartsAt,
            schedule.Value.EventEndsAt
        );
        if (await activities.AnyOutsideRangeAsync(id, lowerInclusive, upperExclusive, ct))
        {
            return Error.Validation();
        }

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure)
        {
            return thumbnail.Error!;
        }

        ev.Title = request.Title.Trim();
        ev.Subtitle = request.Subtitle.Trim();
        ev.Description = request.Description;
        ev.EventStartsAt = schedule.Value.EventStartsAt;
        ev.EventEndsAt = schedule.Value.EventEndsAt;
        ev.SignupStartsAt = schedule.Value.SignupStartsAt;
        ev.SignupEndsAt = schedule.Value.SignupEndsAt;
        ev.ThumbnailId = request.ThumbnailId;
        ev.UpdatedAt = DateTimeOffset.UtcNow;
        ev.UpdatedBy = userId;

        events.Update(ev);
        await uow.SaveChangesAsync(ct);

        return ev.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await events.ExistsAsync(e => e.Id == id, ct))
        {
            return Error.NotFound();
        }

        await events.RemoveAsync(e => e.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<EventResponse>> SetFeaturedAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        if (!await events.ExistsAsync(e => e.Id == id, ct))
        {
            return Error.NotFound();
        }

        await events.SetFeaturedAsync(id, ct);

        var ev = await events.GetWithThumbnailAsync(id, ct);
        return ev!.ToResponse();
    }

    private async Task<Result> EnsureThumbnailAsync(Guid thumbnailId, CancellationToken ct)
    {
        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct))
        {
            return Error.Validation();
        }

        return Result.Success();
    }

    private static Result<EventSchedule> ValidateSchedule(
        DateOnly? eventStartsAt,
        DateOnly? eventEndsAt,
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
            return Error.Validation();
        }

        if (eventEnd < eventStart || signupEnd <= signupStart)
        {
            return Error.Validation();
        }

        return new EventSchedule(eventStart, eventEnd, signupStart, signupEnd);
    }

    private static (DateTimeOffset LowerInclusive, DateTimeOffset UpperExclusive) DayBounds(
        DateOnly eventStart,
        DateOnly eventEnd
    )
    {
        // UTC-day bounds [lowerInclusive, upperExclusive) covering the event's calendar days,
        // matching ActivityService's DateOnly.FromDateTime(UtcDateTime) comparison.
        var lower = new DateTimeOffset(eventStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var upperExclusive = new DateTimeOffset(
            eventEnd.AddDays(1).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero
        );
        return (lower, upperExclusive);
    }

    private readonly record struct EventSchedule(
        DateOnly EventStartsAt,
        DateOnly EventEndsAt,
        DateTimeOffset SignupStartsAt,
        DateTimeOffset SignupEndsAt
    );
}
