using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Errors;
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

public sealed class CreateActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly CreateActivityCommandHandler sut;

    public CreateActivityCommandHandlerTests()
    {
        sut = new CreateActivityCommandHandler(
            activities,
            new ActivityValidator(events, files, clock),
            currentUser,
            clock
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
            CreateRequest().ToCommand(EventId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventNotFound);
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
            request.ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityScheduleRequired);
    }

    [Fact]
    public async Task HandleAsyncEndNotAfterStartReturnsInvalidRange()
    {
        var ev = EventExists();
        var when = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero);

        var result = await sut.HandleAsync(
            CreateRequest(startsAt: when, endsAt: when).ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityScheduleInvalidRange);
    }

    [Fact]
    public async Task HandleAsyncDatesExceedEventRangeReturnsOutsideEventRange()
    {
        var ev = EventExists();

        var result = await sut.HandleAsync(
            CreateRequest(
                    startsAt: new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                    endsAt: new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero)
                )
                .ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityScheduleOutsideEventRange);
    }

    [Fact]
    public async Task HandleAsyncStartsBeforeEventRangeReturnsOutsideEventRange()
    {
        var ev = EventExists();

        var result = await sut.HandleAsync(
            CreateRequest(
                    startsAt: new DateTimeOffset(2026, 6, 25, 10, 0, 0, TimeSpan.Zero),
                    endsAt: new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero)
                )
                .ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityScheduleOutsideEventRange);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsThumbnailNotFound()
    {
        var ev = EventExists();
        files.ThumbnailExists(false);

        var result = await sut.HandleAsync(
            CreateRequest().ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityThumbnailNotFound);
    }

    [Fact]
    public async Task HandleAsyncModalityMissingReturnsModalityTypeNotFound()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            (CreateRequest() with { ActivityModalityTypeId = Guid.NewGuid() }).ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityModalityTypeNotFound);
    }

    [Fact]
    public async Task HandleAsyncValidRequestStagesTrimmedActivityReturnsId()
    {
        var eventId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        currentUser.Id = UserId.From(caller);
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        events.Finds(NewEvent(id: eventId));
        files.ThumbnailExists(true);
        var added = await CaptureAddedActivitiesAsync();

        var result = await sut.HandleAsync(
            CreateRequest().ToCommand(EventId.From(eventId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Title.Should().Be("Taller");
        created.Location.Should().Be("Sala");
        created.EventId.Value.Should().Be(eventId);
        created.CreatedBy.Value.Should().Be(caller);
        created.CreatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncWithRoleCapacitiesStagesDesiredCounts()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);
        var added = await CaptureAddedActivitiesAsync();

        var result = await sut.HandleAsync(
            CreateRequest(
                    roleCapacities:
                    [
                        new ActivityRoleCapacityRequest(KnownIds.ActivityRoleTypes.Participant, 12),
                        new ActivityRoleCapacityRequest(KnownIds.ActivityRoleTypes.Volunteer, 3),
                    ]
                )
                .ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var saved = added.Should().ContainSingle().Which;
        result.Value.Should().Be(saved.Id);
        saved.RoleCapacities.Should().HaveCount(2);
        saved
            .RoleCapacities.Single(c => c.Role == ActivityRole.Participant)
            .DesiredCount.Should()
            .Be(12);
        saved
            .RoleCapacities.Single(c => c.Role == ActivityRole.Volunteer)
            .DesiredCount.Should()
            .Be(3);
    }

    [Fact]
    public async Task HandleAsyncDuplicatedRoleCapacityRoleReturnsBadRequest()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            CreateRequest(
                    roleCapacities:
                    [
                        new ActivityRoleCapacityRequest(KnownIds.ActivityRoleTypes.Participant, 5),
                        new ActivityRoleCapacityRequest(KnownIds.ActivityRoleTypes.Participant, 8),
                    ]
                )
                .ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ActivityRoleCapacityDuplicated);
    }

    [Fact]
    public async Task HandleAsyncUnknownRoleCapacityRoleReturnsRoleTypeNotFound()
    {
        var ev = EventExists();
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            CreateRequest(roleCapacities: [new ActivityRoleCapacityRequest(Guid.NewGuid(), 5)])
                .ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityRoleTypeNotFound);
    }
}
