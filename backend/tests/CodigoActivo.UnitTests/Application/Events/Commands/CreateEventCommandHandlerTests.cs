using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class CreateEventCommandHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly CreateEventCommandHandler sut;

    public CreateEventCommandHandlerTests()
    {
        sut = new CreateEventCommandHandler(
            events,
            files,
            new EventCategoryChecker(categoryTypes),
            clock,
            uow,
            cacheInvalidator,
            new GetEventByIdQueryHandler(events, new FakeQueryExecutor(), new FakeHybridCache())
        );
    }

    private void CaptureCreatedEvents()
    {
        var store = new List<Event>();
        events.Query().Returns(_ => store.AsQueryable());
        events
            .When(x => x.AddAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var ev = ci.Arg<Event>();
                Assert.NotNull(ev);
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
    }

    public static TheoryData<CreateEventRequest> MissingScheduleDateRequests()
    {
        var eventStart = new DateOnly(2026, 8, 1);
        var eventEnd = new DateOnly(2026, 8, 3);
        var signupStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var signupEnd = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

        var complete = new CreateEventRequest(
            Title: "Hackathon",
            Subtitle: "Innovación",
            Description: "{}",
            EventStartsAt: eventStart,
            EventEndsAt: eventEnd,
            EarlySignupStartsAt: null,
            SignupStartsAt: signupStart,
            SignupEndsAt: signupEnd,
            ThumbnailId: Guid.NewGuid(),
            CategoryTypeIds: [Guid.NewGuid()]
        );

        return
        [
            complete with
            {
                EventStartsAt = null,
            },
            complete with
            {
                EventEndsAt = null,
            },
            complete with
            {
                SignupStartsAt = null,
            },
            complete with
            {
                SignupEndsAt = null,
            },
        ];
    }

    [Theory]
    [MemberData(nameof(MissingScheduleDateRequests))]
    public async Task HandleAsync_MissingScheduleDate_ReturnsScheduleRequired(
        CreateEventRequest request
    )
    {
        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventScheduleRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_EventEndBeforeStart_ReturnsInvalidRange()
    {
        var request = CreateReq(
            eventStart: new DateOnly(2026, 8, 5),
            eventEnd: new DateOnly(2026, 8, 1),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventScheduleInvalidRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_SignupEndNotAfterStart_ReturnsInvalidRange()
    {
        var signup = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var request = CreateReq(
            signupStart: signup,
            signupEnd: signup,
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventScheduleInvalidRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_SignupStartsAfterEventEnd_ReturnsInvalidRange()
    {
        var request = CreateReq(
            eventStart: new DateOnly(2026, 8, 1),
            eventEnd: new DateOnly(2026, 8, 3),
            signupStart: new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
            signupEnd: new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventScheduleInvalidRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_EarlySignupNotBeforeSignupStart_ReturnsEarlySignupNotBeforeSignup()
    {
        var signupStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var request = CreateReq(
            earlySignupStart: signupStart,
            signupStart: signupStart,
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventEarlySignupNotBeforeSignup);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_EarlySignupBeforeSignupStart_PersistsEarlySignupInUtc()
    {
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        CaptureCreatedEvents();

        var request = CreateReq(
            earlySignupStart: new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.FromHours(2)),
            signupStart: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.EarlySignupStartsAt.Should()
            .Be(new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task HandleAsync_ThumbnailMissing_ReturnsThumbnailNotFound()
    {
        files.ThumbnailExists(false);
        var request = CreateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventThumbnailNotFound);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<Event>(), TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_NullCategories_ReturnsCategoriesRequired()
    {
        files.ThumbnailExists(true);
        var request = CreateReq(categoryTypeIds: null);

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventCategoriesRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_EmptyCategories_ReturnsCategoriesRequired()
    {
        files.ThumbnailExists(true);
        var request = CreateReq(categoryTypeIds: []);

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventCategoriesRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_UnknownCategoryTypeId_ReturnsCategoryTypeNotFound()
    {
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        var request = CreateReq(categoryTypeIds: [Guid.NewGuid(), Guid.NewGuid()]);

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsTrimmedEventWithAuditAndCategoriesAndInvalidatesCache()
    {
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        CaptureCreatedEvents();

        var request = CreateReq(categoryTypeIds: [categoryId], thumbnailId: thumbnailId);

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, caller),
            TestContext.Current.CancellationToken
        );

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
                    e != null
                    && e.Title == "Hackathon"
                    && e.Subtitle == "Innovación"
                    && e.CreatedBy == caller
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Events)
                )
            );
    }

    [Fact]
    public async Task HandleAsync_DuplicateCategoryTypeIds_PersistsSingleCategory()
    {
        var categoryId = Guid.NewGuid();
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        CaptureCreatedEvents();

        var request = CreateReq(categoryTypeIds: [categoryId, categoryId]);

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Categories.Should()
            .ContainSingle()
            .Which.CategoryTypeId.Should()
            .Be(categoryId);
        await events
            .Received(1)
            .AddAsync(
                Arg.Is<Event>(e => e != null && e.Categories.Count == 1),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsync_SignupStartsOnEventEndDate_Succeeds()
    {
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        CaptureCreatedEvents();

        var request = CreateReq(
            eventStart: new DateOnly(2026, 8, 1),
            eventEnd: new DateOnly(2026, 8, 3),
            signupStart: new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero),
            signupEnd: new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            new CreateEventCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
