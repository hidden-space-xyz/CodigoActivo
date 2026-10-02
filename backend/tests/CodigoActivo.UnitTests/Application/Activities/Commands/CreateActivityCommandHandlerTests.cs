using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class CreateActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly FakeReadStore readStore = new();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly CreateActivityCommandHandler sut;

    public CreateActivityCommandHandlerTests()
    {
        sut = new CreateActivityCommandHandler(
            activities,
            new ActivityValidator(events, files, readStore, new FakeQueryExecutor(), clock),
            clock,
            uow,
            cacheInvalidator
        );
    }

    private Event EventExists()
    {
        var ev = NewEvent();
        events.Finds(ev);
        return ev;
    }

    private async Task<List<Activity>> CaptureAddedActivitiesAsync()
    {
        var added = new List<Activity>();
        await activities.AddAsync(Arg.Do<Activity>(added.Add), Arg.Any<CancellationToken>());
        return added;
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
            RequestedModalityId,
            startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            roleCapacities
        );
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.Finds(null);

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

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
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

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
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

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
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

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
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

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.ActivityThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncModalityMissingReturnsModalityTypeNotFound()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);
        readStore.ModalityExists(false);

        var result = await sut.HandleAsync(
            new CreateActivityCommand(ev.Id, CreateRequest(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.ActivityModalityTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestPersistsTrimmedActivityReturnsIdAndInvalidatesCache()
    {
        var eventId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        events.Finds(NewEvent(id: eventId));
        files.ThumbnailExists(true);
        readStore.ModalityExists(true);
        var added = await CaptureAddedActivitiesAsync();

        var result = await sut.HandleAsync(
            new CreateActivityCommand(eventId, CreateRequest(), caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Title.Should().Be("Taller");
        created.Location.Should().Be("Sala");
        created.EventId.Should().Be(eventId);
        created.CreatedBy.Should().Be(caller);
        created.CreatedAt.Should().Be(clock.UtcNow);
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
        readStore.ModalityExists(true);
        readStore.CatalogRoles();
        var added = await CaptureAddedActivitiesAsync();

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
        var saved = added.Should().ContainSingle().Which;
        result.Value.Should().Be(saved.Id);
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
    }

    [Fact]
    public async Task HandleAsyncDuplicatedRoleCapacityRoleReturnsBadRequest()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);
        readStore.ModalityExists(true);
        readStore.CatalogRoles();

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

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleCapacityDuplicated);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUnknownRoleCapacityRoleReturnsRoleTypeNotFound()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);
        readStore.ModalityExists(true);
        readStore.CatalogRoles();

        var result = await sut.HandleAsync(
            new CreateActivityCommand(
                ev.Id,
                CreateRequest(roleCapacities: [new ActivityRoleCapacityRequest(Guid.NewGuid(), 5)]),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
