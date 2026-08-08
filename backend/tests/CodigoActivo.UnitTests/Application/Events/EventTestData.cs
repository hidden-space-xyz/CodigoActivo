using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;

namespace CodigoActivo.UnitTests.Application.Events;

internal static class EventTestData
{
    public static Event NewEvent(
        string title = "Hackathon",
        string subtitle = "Innovación",
        DateOnly? starts = null,
        DateOnly? ends = null,
        bool featured = false,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null
    )
    {
        var start = starts ?? new DateOnly(2026, 8, 1);
        var end = ends ?? new DateOnly(2026, 8, 2);
        return new Event
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = subtitle,
            Description = "{}",
            EventStartsAt = start,
            EventEndsAt = end,
            SignupStartsAt = signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt = signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            Featured = featured,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
            Categories = [],
        };
    }

    public static EventCategory NewCategory(
        Guid eventId,
        Guid categoryTypeId,
        string name,
        string color = "#112233"
    )
    {
        return new()
        {
            EventId = eventId,
            EventCategoryTypeId = categoryTypeId,
            EventCategoryType = new EventCategoryType
            {
                Id = categoryTypeId,
                Name = name,
                Color = color,
            },
        };
    }

    public static Event WithCategory(Event ev, Guid categoryTypeId, string name)
    {
        ev.Categories.Add(NewCategory(ev.Id, categoryTypeId, name));
        return ev;
    }

    public static EventCategoryType NewCategoryType(string name, string color = "#000000")
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = color,
        };
    }

    public static CreateEventRequest CreateReq(
        DateOnly? eventStart = null,
        DateOnly? eventEnd = null,
        DateTimeOffset? earlySignupStart = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        IReadOnlyList<Guid>? categoryTypeIds = null,
        Guid? thumbnailId = null
    )
    {
        return new(
            Title: "  Hackathon  ",
            Subtitle: "  Innovación  ",
            Description: "{}",
            EventStartsAt: eventStart ?? new DateOnly(2026, 8, 1),
            EventEndsAt: eventEnd ?? new DateOnly(2026, 8, 3),
            EarlySignupStartsAt: earlySignupStart,
            SignupStartsAt: signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt: signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            ThumbnailId: thumbnailId ?? Guid.NewGuid(),
            CategoryTypeIds: categoryTypeIds
        );
    }

    public static UpdateEventRequest UpdateReq(
        DateOnly? eventStart = null,
        DateOnly? eventEnd = null,
        DateTimeOffset? earlySignupStart = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        IReadOnlyList<Guid>? categoryTypeIds = null,
        Guid? thumbnailId = null,
        string title = "  New title  ",
        string description = "{}"
    )
    {
        return new(
            Title: title,
            Subtitle: "  New subtitle  ",
            Description: description,
            EventStartsAt: eventStart ?? new DateOnly(2026, 8, 1),
            EventEndsAt: eventEnd ?? new DateOnly(2026, 8, 3),
            EarlySignupStartsAt: earlySignupStart,
            SignupStartsAt: signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt: signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            ThumbnailId: thumbnailId ?? Guid.NewGuid(),
            CategoryTypeIds: categoryTypeIds
        );
    }

    public static void HasEvents(this IEventRepository events, params Event[] items)
    {
        events.Query().Returns(items.AsQueryable());
    }

    public static void HasCategoryTypes(
        this IEventCategoryTypeRepository categoryTypes,
        params EventCategoryType[] items
    )
    {
        categoryTypes.Query().Returns(items.AsQueryable());
    }

    public static void HasCategoryCount(this IEventCategoryTypeRepository categoryTypes, int count)
    {
        categoryTypes
            .CountAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(count);
    }

    public static void CategoryTypeNameTaken(
        this IEventCategoryTypeRepository categoryTypes,
        bool taken
    )
    {
        categoryTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(taken);
    }
}
