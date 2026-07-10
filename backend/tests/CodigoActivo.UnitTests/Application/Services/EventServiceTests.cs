using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class EventServiceTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IFileService fileService = Substitute.For<IFileService>();
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly EventService sut;

    public EventServiceTests()
    {
        sut = new EventService(
            events,
            activities,
            files,
            fileService,
            categoryTypes,
            new FakeQueryExecutor(),
            clock,
            uow
        );
    }

    private void HasEvents(params Event[] items) => events.Query().Returns(items.AsQueryable());

    private void HasCategoryTypes(params EventCategoryType[] items) =>
        categoryTypes.Query().Returns(items.AsQueryable());

    private void ThumbnailExists(bool exists) =>
        files
            .ExistsAsync(
                Arg.Any<Expression<Func<FileEntity, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(exists);

    private void HasCategoryCount(int count) =>
        categoryTypes
            .CountAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(count);

    private static Event NewEvent(
        string title = "Hackathon",
        string subtitle = "Innovación",
        DateOnly? starts = null,
        DateOnly? ends = null,
        bool featured = false
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
            SignupStartsAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            Featured = featured,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
            Categories = [],
        };
    }

    private static CreateEventRequest CreateReq(
        DateOnly? eventStart = null,
        DateOnly? eventEnd = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        IReadOnlyList<Guid>? categoryTypeIds = null,
        Guid? thumbnailId = null
    ) =>
        new(
            Title: "  Hackathon  ",
            Subtitle: "  Innovación  ",
            Description: "{}",
            EventStartsAt: eventStart ?? new DateOnly(2026, 8, 1),
            EventEndsAt: eventEnd ?? new DateOnly(2026, 8, 3),
            SignupStartsAt: signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt: signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            ThumbnailId: thumbnailId ?? Guid.NewGuid(),
            CategoryTypeIds: categoryTypeIds
        );

    private static UpdateEventRequest UpdateReq(
        DateOnly? eventStart = null,
        DateOnly? eventEnd = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        IReadOnlyList<Guid>? categoryTypeIds = null,
        Guid? thumbnailId = null,
        string title = "  New title  ",
        string description = "{}"
    ) =>
        new(
            Title: title,
            Subtitle: "  New subtitle  ",
            Description: description,
            EventStartsAt: eventStart ?? new DateOnly(2026, 8, 1),
            EventEndsAt: eventEnd ?? new DateOnly(2026, 8, 3),
            SignupStartsAt: signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt: signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            ThumbnailId: thumbnailId ?? Guid.NewGuid(),
            CategoryTypeIds: categoryTypeIds
        );

    [Fact]
    public async Task ListAsync_NoScope_ProjectsAndPagesAll()
    {
        HasEvents(NewEvent("A"), NewEvent("B"));

        var result = await sut.ListAsync(
            new EventListQuery { Page = 1, PageSize = 10 },
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllBeOfType<EventListItemResponse>();
    }

    [Fact]
    public async Task ListAsync_ScopeUpcoming_KeepsEventsEndingTodayOrLater()
    {
        clock.Today = new DateOnly(2026, 7, 4);
        HasEvents(
            NewEvent("Past", starts: new DateOnly(2026, 1, 1), ends: new DateOnly(2026, 1, 2)),
            NewEvent("Upcoming", starts: new DateOnly(2026, 8, 1), ends: new DateOnly(2026, 8, 2))
        );

        var result = await sut.ListAsync(
            new EventListQuery { Scope = EventScope.Upcoming },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Upcoming");
    }

    [Fact]
    public async Task ListAsync_ScopePast_KeepsEventsEndingBeforeToday()
    {
        clock.Today = new DateOnly(2026, 7, 4);
        HasEvents(
            NewEvent("Past", starts: new DateOnly(2026, 1, 1), ends: new DateOnly(2026, 1, 2)),
            NewEvent("Upcoming", starts: new DateOnly(2026, 8, 1), ends: new DateOnly(2026, 8, 2))
        );

        var result = await sut.ListAsync(
            new EventListQuery { Scope = EventScope.Past },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Past");
    }

    [Fact]
    public async Task ListAsync_YearFilter_KeepsMatchingYear()
    {
        HasEvents(
            NewEvent("Y2025", starts: new DateOnly(2025, 5, 1), ends: new DateOnly(2025, 5, 2)),
            NewEvent("Y2026", starts: new DateOnly(2026, 5, 1), ends: new DateOnly(2026, 5, 2))
        );

        var result = await sut.ListAsync(
            new EventListQuery { Year = 2025 },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Y2025");
    }

    [Fact]
    public async Task ListAsync_FeaturedFilter_KeepsFeaturedOnly()
    {
        HasEvents(NewEvent("Plain", featured: false), NewEvent("Star", featured: true));

        var result = await sut.ListAsync(
            new EventListQuery { Featured = true },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Star");
    }

    [Fact]
    public async Task ListAsync_TitleSearch_IsAccentAndCaseInsensitive()
    {
        HasEvents(NewEvent("Festival Ávila"), NewEvent("Concierto"));

        var result = await sut.ListAsync(
            new EventListQuery { Title = "avila" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Festival Ávila");
    }

    [Fact]
    public async Task ListAsync_SubtitleSearch_MatchesSubstring()
    {
        HasEvents(
            NewEvent("A", subtitle: "Talleres de robótica"),
            NewEvent("B", subtitle: "Charlas")
        );

        var result = await sut.ListAsync(
            new EventListQuery { Subtitle = "robotica" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("A");
    }

    [Fact]
    public async Task ListAsync_DescendingTitleSort_OrdersResults()
    {
        HasEvents(NewEvent("Alpha"), NewEvent("Zeta"), NewEvent("Mint"));

        var result = await sut.ListAsync(
            new EventListQuery { Sort = "-title" },
            TestContext.Current.CancellationToken
        );

        result.Items.Select(e => e.Title).Should().ContainInOrder("Zeta", "Mint", "Alpha");
    }

    [Fact]
    public async Task ListAsync_SecondPage_SkipsFirstPageItems()
    {
        HasEvents(NewEvent("Alpha"), NewEvent("Mint"), NewEvent("Zeta"));

        var result = await sut.ListAsync(
            new EventListQuery
            {
                Page = 2,
                PageSize = 2,
                Sort = "title",
            },
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(3);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("Zeta");
    }

    [Fact]
    public async Task GetByIdAsync_EventExists_ReturnsEvent()
    {
        var ev = NewEvent();
        HasEvents(ev);

        var result = await sut.GetByIdAsync(ev.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ev.Id);
    }

    [Fact]
    public async Task GetByIdAsync_EventMissing_ReturnsNotFound()
    {
        HasEvents();

        var result = await sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CreateAsync_MissingScheduleDate_ReturnsScheduleRequired(int missing)
    {
        var request = new CreateEventRequest(
            Title: "Hackathon",
            Subtitle: "Innovación",
            Description: "{}",
            EventStartsAt: missing == 0 ? null : new DateOnly(2026, 8, 1),
            EventEndsAt: missing == 1 ? null : new DateOnly(2026, 8, 3),
            SignupStartsAt: missing == 2
                ? null
                : new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt: missing == 3
                ? null
                : new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            ThumbnailId: Guid.NewGuid(),
            CategoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventScheduleRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_EventEndBeforeStart_ReturnsInvalidRange()
    {
        var request = CreateReq(
            eventStart: new DateOnly(2026, 8, 5),
            eventEnd: new DateOnly(2026, 8, 1),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventScheduleInvalidRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_SignupEndNotAfterStart_ReturnsInvalidRange()
    {
        var signup = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var request = CreateReq(
            signupStart: signup,
            signupEnd: signup,
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventScheduleInvalidRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_SignupStartsAfterEventEnd_ReturnsInvalidRange()
    {
        var request = CreateReq(
            eventStart: new DateOnly(2026, 8, 1),
            eventEnd: new DateOnly(2026, 8, 3),
            signupStart: new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
            signupEnd: new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventScheduleInvalidRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ThumbnailMissing_ReturnsThumbnailNotFound()
    {
        ThumbnailExists(false);
        var request = CreateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventThumbnailNotFound);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAsync_NoCategoriesSupplied_ReturnsCategoriesRequired(bool empty)
    {
        ThumbnailExists(true);
        var request = CreateReq(categoryTypeIds: empty ? Array.Empty<Guid>() : null);

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventCategoriesRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_UnknownCategoryTypeId_ReturnsCategoryTypeNotFound()
    {
        ThumbnailExists(true);
        HasCategoryCount(1);
        var request = CreateReq(categoryTypeIds: [Guid.NewGuid(), Guid.NewGuid()]);

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsTrimmedEventWithAuditAndCategories()
    {
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        ThumbnailExists(true);
        HasCategoryCount(1);

        var store = new List<Event>();
        events.Query().Returns(_ => store.AsQueryable());
        events
            .When(x => x.AddAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var ev = ci.Arg<Event>();
                foreach (var category in ev.Categories)
                {
                    category.EventCategoryType = new EventCategoryType
                    {
                        Id = category.EventCategoryTypeId,
                        Name = "Talleres",
                        Color = "#112233",
                    };
                }

                store.Add(ev);
            });

        var request = CreateReq(categoryTypeIds: [categoryId], thumbnailId: thumbnailId);

        var result = await sut.CreateAsync(request, caller, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Hackathon");
        result.Value.Subtitle.Should().Be("Innovación");
        result.Value.CreatedBy.Should().Be(caller);
        result.Value.CreatedAt.Should().Be(clock.UtcNow);
        result.Value.ThumbnailId.Should().Be(thumbnailId);
        result
            .Value.Categories.Should()
            .ContainSingle()
            .Which.CategoryTypeId.Should()
            .Be(categoryId);
        await events
            .Received(1)
            .AddAsync(
                Arg.Is<Event>(e =>
                    e.Title == "Hackathon" && e.Subtitle == "Innovación" && e.CreatedBy == caller
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_InvalidScheduleRange_ReturnsErrorBeforeTouchingRepository()
    {
        var request = UpdateReq(
            eventStart: new DateOnly(2026, 8, 5),
            eventEnd: new DateOnly(2026, 8, 1),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventScheduleInvalidRange);
        await events
            .DidNotReceiveWithAnyArgs()
            .GetForEditAsync(Guid.Empty, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_NoCategoriesSupplied_ReturnsCategoriesRequired()
    {
        var request = UpdateReq(categoryTypeIds: null);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventCategoriesRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_EventMissing_ReturnsNotFound()
    {
        HasCategoryCount(1);
        events.GetForEditAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Event?)null);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ActivityOutsideNewRange_ReturnsActivitiesOutsideRange()
    {
        var ev = NewEvent();
        HasCategoryCount(1);
        events.GetForEditAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        activities
            .AnyOutsideRangeAsync(
                ev.Id,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.UpdateAsync(
            ev.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventActivitiesOutsideNewRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailMissing_ReturnsThumbnailNotFound()
    {
        var ev = NewEvent();
        HasCategoryCount(1);
        events.GetForEditAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        activities
            .AnyOutsideRangeAsync(
                ev.Id,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);
        ThumbnailExists(false);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.UpdateAsync(
            ev.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_ReplacesCategoriesAndPersists()
    {
        var caller = Guid.NewGuid();
        var newCategoryId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var ev = NewEvent("Old title", "Old subtitle");
        ev.Categories.Add(
            new EventCategory { EventId = ev.Id, EventCategoryTypeId = Guid.NewGuid() }
        );
        var store = new List<Event> { ev };
        events.Query().Returns(_ => store.AsQueryable());
        events.GetForEditAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        activities
            .AnyOutsideRangeAsync(
                ev.Id,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);
        ThumbnailExists(true);
        HasCategoryCount(1);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                foreach (var category in ev.Categories)
                {
                    category.EventCategoryType ??= new EventCategoryType
                    {
                        Id = category.EventCategoryTypeId,
                        Name = "Charlas",
                        Color = "#654321",
                    };
                }

                return 1;
            });

        var request = UpdateReq(
            categoryTypeIds: [newCategoryId],
            thumbnailId: thumbnailId,
            title: "  New title  "
        );

        var result = await sut.UpdateAsync(
            ev.Id,
            request,
            caller,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        ev.Title.Should().Be("New title");
        ev.Subtitle.Should().Be("New subtitle");
        ev.ThumbnailId.Should().Be(thumbnailId);
        ev.UpdatedBy.Should().Be(caller);
        ev.UpdatedAt.Should().Be(clock.UtcNow);
        ev.Categories.Should().ContainSingle().Which.EventCategoryTypeId.Should().Be(newCategoryId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_SameThumbnail_DoesNotCleanUp()
    {
        var ev = NewEvent();
        PrepareUpdate(ev);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()], thumbnailId: ev.ThumbnailId);

        var result = await sut.UpdateAsync(
            ev.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await fileService
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(Guid.Empty, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ImagesDroppedFromDescription_CleansUpRemovedKeepsRest()
    {
        var ev = NewEvent();
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        ev.Description =
            $"{{\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}";
        PrepareUpdate(ev);
        var request = UpdateReq(
            categoryTypeIds: [Guid.NewGuid()],
            thumbnailId: ev.ThumbnailId,
            description: $"{{\"b\":\"/api/files/{keptId}/content\"}}"
        );

        var result = await sut.UpdateAsync(
            ev.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(removedId, Arg.Any<CancellationToken>());
        await fileService
            .DidNotReceive()
            .DeleteIfOrphanedAsync(keptId, Arg.Any<CancellationToken>());
    }

    private void PrepareUpdate(Event ev)
    {
        HasEvents(ev);
        events.GetForEditAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        ThumbnailExists(true);
        HasCategoryCount(1);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                foreach (var category in ev.Categories)
                {
                    category.EventCategoryType ??= new EventCategoryType
                    {
                        Id = category.EventCategoryTypeId,
                        Name = "Charlas",
                        Color = "#654321",
                    };
                }

                return 1;
            });
    }

    [Fact]
    public async Task DeleteAsync_EventMissing_ReturnsNotFound()
    {
        events
            .FindAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Event?)null);

        var result = await sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await fileService
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(Guid.Empty, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAsync_ValidEvent_RemovesAndCleansThumbnailsOnceEach()
    {
        var ev = NewEvent();
        events
            .FindAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ev);
        var sharedActivityThumbnailId = Guid.NewGuid();
        var foreignActivity = new Activity
        {
            EventId = Guid.NewGuid(),
            ThumbnailId = Guid.NewGuid(),
        };
        activities
            .Query()
            .Returns(
                new[]
                {
                    new Activity { EventId = ev.Id, ThumbnailId = sharedActivityThumbnailId },
                    new Activity { EventId = ev.Id, ThumbnailId = sharedActivityThumbnailId },
                    foreignActivity,
                }.AsQueryable()
            );

        var result = await sut.DeleteAsync(ev.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        events.Received(1).Remove(ev);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(ev.ThumbnailId, Arg.Any<CancellationToken>());
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(sharedActivityThumbnailId, Arg.Any<CancellationToken>());
        await fileService
            .DidNotReceive()
            .DeleteIfOrphanedAsync(foreignActivity.ThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ImagesEmbeddedInDescription_CleansThemUp()
    {
        var ev = NewEvent();
        var embeddedId = Guid.NewGuid();
        ev.Description = $"{{\"img\":\"/api/files/{embeddedId}/content\"}}";
        events
            .FindAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ev);
        activities.Query().Returns(Array.Empty<Activity>().AsQueryable());

        var result = await sut.DeleteAsync(ev.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(embeddedId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetFeaturedAsync_EventMissing_ReturnsNotFound()
    {
        events.SetFeaturedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await sut.SetFeaturedAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task SetFeaturedAsync_EventExists_ReturnsFeaturedEvent()
    {
        var ev = NewEvent(featured: true);
        HasEvents(ev);
        events.SetFeaturedAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await sut.SetFeaturedAsync(ev.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ev.Id);
        result.Value.Featured.Should().BeTrue();
    }

    [Fact]
    public async Task ListCategoryTypesAsync_MultipleTypes_ReturnsOrderedByName()
    {
        HasCategoryTypes(
            new EventCategoryType
            {
                Id = Guid.NewGuid(),
                Name = "Zeta",
                Color = "#000000",
            },
            new EventCategoryType
            {
                Id = Guid.NewGuid(),
                Name = "Alpha",
                Color = "#ffffff",
            }
        );

        var result = await sut.ListCategoryTypesAsync(TestContext.Current.CancellationToken);

        result.Select(c => c.Name).Should().ContainInOrder("Alpha", "Zeta");
    }

    [Fact]
    public async Task CreateCategoryTypeAsync_NameExists_ReturnsConflict()
    {
        categoryTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var result = await sut.CreateCategoryTypeAsync(
            new CreateEventCategoryTypeRequest("  Talleres  ", "  #112233  "),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNameAlreadyExists);
        await categoryTypes
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateCategoryTypeAsync_ValidRequest_PersistsTrimmedType()
    {
        categoryTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await sut.CreateCategoryTypeAsync(
            new CreateEventCategoryTypeRequest("  Talleres  ", "  #112233  "),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Talleres");
        result.Value.Color.Should().Be("#112233");
        await categoryTypes
            .Received(1)
            .AddAsync(
                Arg.Is<EventCategoryType>(c => c.Name == "Talleres" && c.Color == "#112233"),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCategoryTypeAsync_TypeMissing_ReturnsNotFound()
    {
        categoryTypes
            .FindAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((EventCategoryType?)null);

        var result = await sut.UpdateCategoryTypeAsync(
            Guid.NewGuid(),
            new UpdateEventCategoryTypeRequest("Talleres", "#112233"),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateCategoryTypeAsync_NameTakenByAnother_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        var existing = new EventCategoryType
        {
            Id = id,
            Name = "Old",
            Color = "#000000",
        };
        categoryTypes
            .FindAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(existing);
        categoryTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var result = await sut.UpdateCategoryTypeAsync(
            id,
            new UpdateEventCategoryTypeRequest("Talleres", "#112233"),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNameAlreadyExists);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateCategoryTypeAsync_ValidRequest_MutatesAndPersists()
    {
        var id = Guid.NewGuid();
        var existing = new EventCategoryType
        {
            Id = id,
            Name = "Old",
            Color = "#000000",
        };
        categoryTypes
            .FindAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(existing);
        categoryTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await sut.UpdateCategoryTypeAsync(
            id,
            new UpdateEventCategoryTypeRequest("  New  ", "  #abcdef  "),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("New");
        result.Value.Color.Should().Be("#abcdef");
        existing.Name.Should().Be("New");
        existing.Color.Should().Be("#abcdef");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCategoryTypeAsync_NothingRemoved_ReturnsNotFound()
    {
        categoryTypes
            .RemoveAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(0);

        var result = await sut.DeleteCategoryTypeAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteCategoryTypeAsync_Removed_SavesChanges()
    {
        categoryTypes
            .RemoveAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1);

        var result = await sut.DeleteCategoryTypeAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
