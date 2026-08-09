using AwesomeAssertions;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class UpdateActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IActivityModalityTypeRepository modalityTypes =
        Substitute.For<IActivityModalityTypeRepository>();
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdateActivityCommandHandler sut;

    public UpdateActivityCommandHandlerTests()
    {
        var executor = new FakeQueryExecutor();
        sut = new UpdateActivityCommandHandler(
            activities,
            new ActivityValidator(events, files, modalityTypes, roleTypes, executor, clock),
            orphanCleaner,
            clock,
            uow,
            cacheInvalidator,
            new GetActivityByIdQueryHandler(activities, executor, new FakeHybridCache())
        );
    }

    private void EventExistsFor(Activity activity)
    {
        events.HasEvents(NewEvent(id: activity.EventId));
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
            Guid.NewGuid(),
            startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            thumbnailId ?? Guid.NewGuid(),
            roleCapacities
        );
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsNotFound()
    {
        activities.ActivityFound(null);

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
        activities.ActivityFound(activity);
        events.HasEvents();

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
        activities.ActivityFound(activity);
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

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsThumbnailNotFound()
    {
        var activity = NewActivity();
        activities.ActivityFound(activity);
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
        activities.ActivityFound(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(false);

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
        var activityId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var activity = NewActivity(title: "Old", id: activityId, eventId: eventId);

        var stored = new List<Activity> { activity };
        activities.Query().Returns(_ => stored.AsQueryable());
        activities.ActivityFound(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(true);

        var result = await sut.HandleAsync(
            new UpdateActivityCommand(activityId, UpdateRequest(title: "  New  "), caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("New");
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
        var activity = NewActivity();
        activity.RoleCapacities =
        [
            Capacity(activity.Id, SeedIds.ActivityRoleTypes.Participant, 5),
            Capacity(activity.Id, SeedIds.ActivityRoleTypes.Leader, 1),
        ];
        activities.HasActivities(activity);
        activities.ActivityFound(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(true);
        roleTypes.HasRoleCatalog();

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
        var activity = NewActivity();
        activity.RoleCapacities = [Capacity(activity.Id, SeedIds.ActivityRoleTypes.Participant, 5)];
        activities.HasActivities(activity);
        activities.ActivityFound(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(true);

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
        activities.HasActivities(activity);
        activities.ActivityFound(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(true);

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
        activities.HasActivities(activity);
        activities.ActivityFound(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);
        modalityTypes.ModalityExists(true);

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
