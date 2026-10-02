using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class UpdateActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly FakeReadStore readStore = new();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdateActivityCommandHandler sut;

    public UpdateActivityCommandHandlerTests()
    {
        sut = new UpdateActivityCommandHandler(
            activities,
            new ActivityValidator(events, files, readStore, new FakeQueryExecutor(), clock),
            orphanCleaner,
            clock,
            uow,
            cacheInvalidator
        );
    }

    private void EventExistsFor(Activity activity)
    {
        events
            .GetByIdAsync(activity.EventId, Arg.Any<CancellationToken>())
            .Returns(NewEvent(id: activity.EventId));
    }

    private static UpdateActivityRequest UpdateRequest(
        string title = "  New  ",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        Guid? thumbnailId = null,
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
            thumbnailId ?? Guid.NewGuid(),
            roleCapacities
        );
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsNotFound()
    {
        activities.Finds(null);

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(Guid.NewGuid(), UpdateRequest(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncParentEventMissingReturnsEventNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        events.Finds(null);

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(activity.Id, UpdateRequest(), Guid.NewGuid()),
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
        var activity = NewActivity();
        activities.Finds(activity);
        EventExistsFor(activity);

        var request = new UpdateActivityRequest(
            "  New  ",
            "{}",
            "  Sala  ",
            Guid.NewGuid(),
            null,
            new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            null
        );

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(activity.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsThumbnailNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(false);

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(activity.Id, UpdateRequest(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.ActivityThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncModalityMissingReturnsModalityTypeNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        readStore.ModalityExists(false);

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(activity.Id, UpdateRequest(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.ActivityModalityTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestMutatesPersistsAndInvalidatesCache()
    {
        var eventId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var activity = NewActivity(title: "Old", eventId: eventId);
        var activityId = activity.Id;
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        readStore.ModalityExists(true);

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(activityId, UpdateRequest(title: "  New  "), caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.Title.Should().Be("New");
        activity.UpdatedBy.Should().Be(caller);
        activity.UpdatedAt.Should().Be(clock.UtcNow);
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
    public async Task HandleAsyncWithRoleCapacitiesSyncsCollection()
    {
        var activity = NewActivity(
            capacities:
            [
                new RoleCapacity(SeedIds.ActivityRoleTypes.Participant, 5),
                new RoleCapacity(SeedIds.ActivityRoleTypes.Leader, 1),
            ]
        );
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        readStore.ModalityExists(true);
        readStore.CatalogRoles();

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(
                activity.Id,
                UpdateRequest(
                    roleCapacities:
                    [
                        new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 2),
                        new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Volunteer, 4),
                    ]
                ),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.RoleCapacities.Should().HaveCount(2);
        activity
            .RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
            )
            .DesiredCount.Should()
            .Be(2);
        activity
            .RoleCapacities.Single(c => c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer)
            .DesiredCount.Should()
            .Be(4);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncNullRoleCapacitiesClearsExisting()
    {
        var activity = NewActivity(
            capacities: [new RoleCapacity(SeedIds.ActivityRoleTypes.Participant, 5)]
        );
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        readStore.ModalityExists(true);

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(activity.Id, UpdateRequest(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.RoleCapacities.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncReplacingThumbnailCleansUpPreviousFileAfterSave()
    {
        var activity = NewActivity();
        var previousThumbnailId = activity.ThumbnailId;
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        readStore.ModalityExists(true);

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(
                activity.Id,
                UpdateRequest(thumbnailId: Guid.NewGuid()),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteIfOrphanedAsync(previousThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncKeepingSameThumbnailDoesNotCleanUp()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        readStore.ModalityExists(true);

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(
                activity.Id,
                UpdateRequest(thumbnailId: activity.ThumbnailId),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(Guid.Empty, TestContext.Current.CancellationToken);
    }
}
