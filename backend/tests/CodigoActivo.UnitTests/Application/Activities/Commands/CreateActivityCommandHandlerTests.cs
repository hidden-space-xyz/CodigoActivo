using AwesomeAssertions;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class CreateActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IActivityModalityTypeRepository modalityTypes =
        Substitute.For<IActivityModalityTypeRepository>();
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly CreateActivityCommandHandler sut;

    public CreateActivityCommandHandlerTests()
    {
        var executor = new FakeQueryExecutor();
        sut = new CreateActivityCommandHandler(
            activities,
            new ActivityValidator(events, files, modalityTypes, roleTypes, executor, clock),
            clock,
            uow,
            cacheInvalidator,
            new GetActivityByIdQueryHandler(activities, executor, new FakeHybridCache())
        );
    }

    private Event EventExists()
    {
        var ev = NewEvent();
        events.HasEvents(ev);
        return ev;
    }

    private static bool IsCreatedActivity(
        Activity? activity,
        Guid eventId,
        Guid caller,
        DateTimeOffset createdAt
    )
    {
        if (activity is null)
        {
            return false;
        }

        var isTrimmed =
            string.Equals(activity.Title, "Taller", StringComparison.Ordinal)
            && string.Equals(activity.Location, "Sala", StringComparison.Ordinal);
        return isTrimmed
            && activity.EventId == eventId
            && activity.CreatedBy == caller
            && activity.CreatedAt == createdAt;
    }

    private static CreateActivityRequest CreateRequest(
        string title = "  Taller  ",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        IReadOnlyList<ActivityRoleCapacityRequest>? roleCapacities = null
    )
    {
        return new(
            title,
            "{}",
            "  Sala  ",
            Guid.NewGuid(),
            startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            roleCapacities
        );
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.HasEvents();

        var result = await sut.HandleAsync(
            new CreateActivityCommand(Guid.NewGuid(), CreateRequest(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncStartMissingReturnsScheduleRequired()
    {
        var ev = EventExists();

        var request = new CreateActivityRequest(
            "  Taller  ",
            "{}",
            "  Sala  ",
            Guid.NewGuid(),
            null,
            new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            null
        );

        var result = await sut.HandleAsync(
            new CreateActivityCommand(ev.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncEndNotAfterStartReturnsInvalidRange()
    {
        var ev = EventExists();
        var when = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero);

        var result = await sut.HandleAsync(
            new CreateActivityCommand(
                ev.Id,
                CreateRequest(startsAt: when, endsAt: when),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleInvalidRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncDatesExceedEventRangeReturnsOutsideEventRange()
    {
        var ev = EventExists();

        var result = await sut.HandleAsync(
            new CreateActivityCommand(
                ev.Id,
                CreateRequest(
                    startsAt: new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                    endsAt: new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero)
                ),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleOutsideEventRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncStartsBeforeEventRangeReturnsOutsideEventRange()
    {
        var ev = EventExists();

        var result = await sut.HandleAsync(
            new CreateActivityCommand(
                ev.Id,
                CreateRequest(
                    startsAt: new DateTimeOffset(2026, 6, 25, 10, 0, 0, TimeSpan.Zero),
                    endsAt: new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero)
                ),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleOutsideEventRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsThumbnailNotFound()
    {
        var ev = EventExists();
        files.ThumbnailExists(false);

        var result = await sut.HandleAsync(
            new CreateActivityCommand(ev.Id, CreateRequest(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncModalityMissingReturnsModalityTypeNotFound()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(false);

        var result = await sut.HandleAsync(
            new CreateActivityCommand(ev.Id, CreateRequest(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityModalityTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestPersistsTrimmedActivityReturnsProjectionAndInvalidatesCache()
    {
        var eventId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        events.HasEvents(NewEvent(id: eventId));
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(true);

        var stored = new List<Activity>();
        activities.Query().Returns(_ => stored.AsQueryable());
        activities
            .When(a => a.AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var a = ci.Arg<Activity>();
                Assert.NotNull(a);
                a.ActivityModalityType = new ActivityModalityType { Name = "Presencial" };
                stored.Add(a);
            });

        var result = await sut.HandleAsync(
            new CreateActivityCommand(eventId, CreateRequest(), caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Taller");
        result.Value.Location.Should().Be("Sala");
        result.Value.EventId.Should().Be(eventId);
        await activities
            .Received(1)
            .AddAsync(
                Arg.Is<Activity>(a => IsCreatedActivity(a, eventId, caller, clock.UtcNow)),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Activities)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncWithRoleCapacitiesPersistsDesiredCounts()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(true);
        roleTypes.HasRoleCatalog();

        var stored = new List<Activity>();
        activities.Query().Returns(_ => stored.AsQueryable());
        activities
            .When(a => a.AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var a = ci.Arg<Activity>();
                Assert.NotNull(a);
                a.ActivityModalityType = new ActivityModalityType { Name = "Presencial" };
                stored.Add(a);
            });

        var result = await sut.HandleAsync(
            new CreateActivityCommand(
                ev.Id,
                CreateRequest(
                    roleCapacities:
                    [
                        new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 12),
                        new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Volunteer, 3),
                    ]
                ),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var saved = stored.Single();
        saved.RoleCapacities.Should().HaveCount(2);
        saved
            .RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
            )
            .DesiredCount.Should()
            .Be(12);
        saved
            .RoleCapacities.Single(c => c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer)
            .DesiredCount.Should()
            .Be(3);
        result
            .Value.RoleCapacities.Should()
            .BeEquivalentTo([
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Participant, 12, false),
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Volunteer, 3, false),
            ]);
    }

    [Fact]
    public async Task HandleAsyncDuplicatedRoleCapacityRoleReturnsBadRequest()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(true);
        roleTypes.HasRoleCatalog();

        var result = await sut.HandleAsync(
            new CreateActivityCommand(
                ev.Id,
                CreateRequest(
                    roleCapacities:
                    [
                        new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 5),
                        new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 8),
                    ]
                ),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleCapacityDuplicated);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUnknownRoleCapacityRoleReturnsRoleTypeNotFound()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(true);
        roleTypes.HasRoleCatalog();

        var result = await sut.HandleAsync(
            new CreateActivityCommand(
                ev.Id,
                CreateRequest(roleCapacities: [new ActivityRoleCapacityRequest(Guid.NewGuid(), 5)]),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
