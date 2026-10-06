using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
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
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly UpdateActivityCommandHandler sut;

    public UpdateActivityCommandHandlerTests()
    {
        sut = new UpdateActivityCommandHandler(
            activities,
            new ActivityValidator(events, files, clock),
            currentUser,
            clock
        );
    }

    private void EventExistsFor(Activity activity)
    {
        events
            .GetByIdAsync(activity.EventId, Arg.Any<CancellationToken>())
            .Returns(NewEvent(id: activity.EventId.Value));
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
            UpdateRequest().ToCommand(ActivityId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task HandleAsyncParentEventMissingReturnsEventNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        events.Finds(null);

        var result = await sut.HandleAsync(
            UpdateRequest().ToCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventNotFound);
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
            request.ToCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityScheduleRequired);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsThumbnailNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(false);

        var result = await sut.HandleAsync(
            UpdateRequest().ToCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ApplicationErrorCode.ActivityThumbnailNotFound);
    }

    [Fact]
    public async Task HandleAsyncModalityMissingReturnsModalityTypeNotFound()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            (UpdateRequest() with { ActivityModalityTypeId = Guid.NewGuid() }).ToCommand(
                activity.Id
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ApplicationErrorCode.ActivityModalityTypeNotFound);
    }

    [Fact]
    public async Task HandleAsyncValidRequestMutatesStages()
    {
        var eventId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        currentUser.Id = UserId.From(caller);
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var activity = NewActivity(title: "Old", eventId: eventId);
        var activityId = activity.Id;
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            UpdateRequest(title: "  New  ").ToCommand(activityId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.Title.Should().Be("New");
        activity.UpdatedBy.Should().Be(UserId.From(caller));
        activity.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncWithRoleCapacitiesSyncsCollection()
    {
        var activity = NewActivity(
            capacities:
            [
                new RoleCapacity(ActivityRole.Participant, 5),
                new RoleCapacity(ActivityRole.Leader, 1),
            ]
        );
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            UpdateRequest(
                    roleCapacities:
                    [
                        new ActivityRoleCapacityRequest(KnownIds.ActivityRoleTypes.Participant, 2),
                        new ActivityRoleCapacityRequest(KnownIds.ActivityRoleTypes.Volunteer, 4),
                    ]
                )
                .ToCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.RoleCapacities.Should().HaveCount(2);
        activity
            .RoleCapacities.Single(c => c.Role == ActivityRole.Participant)
            .DesiredCount.Should()
            .Be(2);
        activity
            .RoleCapacities.Single(c => c.Role == ActivityRole.Volunteer)
            .DesiredCount.Should()
            .Be(4);
    }

    [Fact]
    public async Task HandleAsyncNullRoleCapacitiesClearsExisting()
    {
        var activity = NewActivity(capacities: [new RoleCapacity(ActivityRole.Participant, 5)]);
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            UpdateRequest().ToCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.RoleCapacities.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncReplacingThumbnailReleasesPreviousFile()
    {
        var activity = NewActivity();
        var previousThumbnailId = activity.ThumbnailId;
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            UpdateRequest(thumbnailId: Guid.NewGuid()).ToCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        DomainEvents.ReleasedFiles(activity).Should().Equal(previousThumbnailId);
    }

    [Fact]
    public async Task HandleAsyncKeepingSameThumbnailDoesNotCleanUp()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        EventExistsFor(activity);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            UpdateRequest(thumbnailId: activity.ThumbnailId.Value).ToCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        DomainEvents.ReleasedFiles(activity).Should().BeEmpty();
    }
}
