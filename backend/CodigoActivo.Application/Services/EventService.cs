using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class EventService(
    IEventRepository events,
    IActivityRepository activities,
    IFileRepository files,
    IEventCategoryTypeRepository categoryTypes,
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow
) : IEventService
{
    private static readonly SortMap<EventResponse> Sort = new SortMap<EventResponse>()
        .Add("eventStartsAt", e => e.EventStartsAt)
        .Add("eventEndsAt", e => e.EventEndsAt)
        .Add("createdAt", e => e.CreatedAt)
        .Add("title", e => e.Title)
        .Add("subtitle", e => e.Subtitle)
        .Add("featured", e => e.Featured)
        .Default("eventStartsAt")
        .Tie(e => e.Id);

    public Task<PagedResult<EventResponse>> ListAsync(
        EventListQuery query,
        CancellationToken ct = default
    )
    {
        var today = clock.Today;
        var source = events.Query().Select(Projections.Event);

        source = query.Scope switch
        {
            EventScope.Upcoming => source.Where(e => e.EventEndsAt >= today),
            EventScope.Past => source.Where(e => e.EventEndsAt < today),
            _ => source,
        };

        if (query.Year is { } year) source = source.Where(e => e.EventStartsAt.Year == year);
        if (query.Featured is { } featured) source = source.Where(e => e.Featured == featured);
        if (!string.IsNullOrWhiteSpace(query.Title))
            source = source.Where(
                TextSearch.Contains<EventResponse>(e => e.Title, TextSearch.Normalize(query.Title))
            );
        if (!string.IsNullOrWhiteSpace(query.Subtitle))
            source = source.Where(
                TextSearch.Contains<EventResponse>(
                    e => e.Subtitle,
                    TextSearch.Normalize(query.Subtitle)
                )
            );

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public async Task<Result<EventResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == id).Select(Projections.Event),
            ct
        );
        if (response is null) return Error.NotFound(ErrorCode.EventNotFound);
        return response;
    }

    public async Task<IReadOnlyList<int>> GetPastYearsAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        return await executor.ToListAsync(
            events
                .Query()
                .Where(e => e.EventEndsAt < today)
                .Select(e => e.EventStartsAt.Year)
                .Distinct()
                .OrderByDescending(year => year),
            ct
        );
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
        if (schedule.IsFailure) return schedule.Error!;

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure) return thumbnail.Error!;

        var categories = await EnsureCategoriesAsync(request.CategoryTypeIds, ct);
        if (categories.IsFailure) return categories.Error!;

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
        ApplyCategories(ev, request.CategoryTypeIds!);

        await events.AddAsync(ev, ct);
        await uow.SaveChangesAsync(ct);

        var created = await events.GetWithCategoriesAsync(ev.Id, ct);
        return created!.ToResponse();
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
        if (schedule.IsFailure) return schedule.Error!;

        var categories = await EnsureCategoriesAsync(request.CategoryTypeIds, ct);
        if (categories.IsFailure) return categories.Error!;

        var ev = await events.GetForEditAsync(id, ct);
        if (ev is null) return Error.NotFound(ErrorCode.EventNotFound);

        var (lowerInclusive, upperExclusive) = DayBounds(
            schedule.Value.EventStartsAt,
            schedule.Value.EventEndsAt
        );
        if (await activities.AnyOutsideRangeAsync(id, lowerInclusive, upperExclusive, ct))
            return Error.BadRequest(ErrorCode.EventActivitiesOutsideNewRange);

        var thumbnail = await EnsureThumbnailAsync(request.ThumbnailId, ct);
        if (thumbnail.IsFailure) return thumbnail.Error!;

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

        ev.Categories.Clear();
        ApplyCategories(ev, request.CategoryTypeIds!);

        await uow.SaveChangesAsync(ct);

        var updated = await events.GetWithCategoriesAsync(id, ct);
        return updated!.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await events.ExistsAsync(e => e.Id == id, ct)) return Error.NotFound(ErrorCode.EventNotFound);

        await events.RemoveAsync(e => e.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<EventResponse>> SetFeaturedAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        if (!await events.ExistsAsync(e => e.Id == id, ct)) return Error.NotFound(ErrorCode.EventNotFound);

        await events.SetFeaturedAsync(id, ct);

        var ev = await events.GetWithThumbnailAsync(id, ct);
        return ev!.ToResponse();
    }

    public async Task<IReadOnlyList<EventCategoryTypeResponse>> ListCategoryTypesAsync(
        CancellationToken ct = default
    )
    {
        return await executor.ToListAsync(
            categoryTypes.Query().OrderBy(c => c.Name).Select(Projections.EventCategoryType),
            ct
        );
    }

    public async Task<Result<EventCategoryTypeResponse>> CreateCategoryTypeAsync(
        CreateEventCategoryTypeRequest request,
        CancellationToken ct = default
    )
    {
        var name = request.Name.Trim();
        if (await categoryTypes.ExistsAsync(x => x.Name == name, ct))
            return Error.Conflict(ErrorCode.EventCategoryTypeNameAlreadyExists);

        var categoryType = new EventCategoryType { Name = name, Color = request.Color.Trim() };
        await categoryTypes.AddAsync(categoryType, ct);
        await uow.SaveChangesAsync(ct);
        return categoryType.ToResponse();
    }

    public async Task<Result<EventCategoryTypeResponse>> UpdateCategoryTypeAsync(
        Guid id,
        UpdateEventCategoryTypeRequest request,
        CancellationToken ct = default
    )
    {
        var categoryType = await categoryTypes.FindAsync(x => x.Id == id, ct);
        if (categoryType is null) return Error.NotFound(ErrorCode.EventCategoryTypeNotFound);

        var name = request.Name.Trim();
        if (await categoryTypes.ExistsAsync(x => x.Name == name && x.Id != id, ct))
            return Error.Conflict(ErrorCode.EventCategoryTypeNameAlreadyExists);

        categoryType.Name = name;
        categoryType.Color = request.Color.Trim();
        categoryTypes.Update(categoryType);
        await uow.SaveChangesAsync(ct);
        return categoryType.ToResponse();
    }

    public async Task<Result> DeleteCategoryTypeAsync(Guid id, CancellationToken ct = default)
    {
        if (!await categoryTypes.ExistsAsync(x => x.Id == id, ct))
            return Error.NotFound(ErrorCode.EventCategoryTypeNotFound);

        await categoryTypes.RemoveAsync(x => x.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> EnsureThumbnailAsync(Guid thumbnailId, CancellationToken ct)
    {
        if (!await files.ExistsAsync(f => f.Id == thumbnailId, ct))
            return Error.BadRequest(ErrorCode.EventThumbnailNotFound);

        return Result.Success();
    }

    private async Task<Result> EnsureCategoriesAsync(
        IReadOnlyList<Guid>? categoryTypeIds,
        CancellationToken ct
    )
    {
        if (categoryTypeIds is null || categoryTypeIds.Count == 0)
            return Error.BadRequest(ErrorCode.EventCategoriesRequired);

        var distinct = categoryTypeIds.Distinct().ToList();
        var existing = await categoryTypes.CountAsync(c => distinct.Contains(c.Id), ct);
        if (existing != distinct.Count) return Error.BadRequest(ErrorCode.EventCategoryTypeNotFound);

        return Result.Success();
    }

    private static void ApplyCategories(Event ev, IReadOnlyList<Guid> categoryTypeIds)
    {
        foreach (var categoryTypeId in categoryTypeIds.Distinct())
            ev.Categories.Add(
                new EventCategory { EventId = ev.Id, EventCategoryTypeId = categoryTypeId }
            );
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
            return Error.BadRequest(ErrorCode.EventScheduleRequired);

        if (eventEnd < eventStart || signupEnd <= signupStart)
            return Error.BadRequest(ErrorCode.EventScheduleInvalidRange);

        if (DateOnly.FromDateTime(signupStart.UtcDateTime) > eventEnd)
            return Error.BadRequest(ErrorCode.EventScheduleInvalidRange);

        return new EventSchedule(eventStart, eventEnd, signupStart, signupEnd);
    }

    private static (DateTimeOffset LowerInclusive, DateTimeOffset UpperExclusive) DayBounds(
        DateOnly eventStart,
        DateOnly eventEnd
    )
    {
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