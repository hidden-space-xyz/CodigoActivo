using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Participation.Commands;

public sealed class SaveEventRatingCommandHandlerTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChildId = Guid.NewGuid();
    private static readonly Guid ActivityId = Guid.NewGuid();

    private static readonly SaveEventRatingRequest ValidRequest = new(
        5,
        "Bien",
        "La cola",
        "Más talleres"
    );

    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IEventRatingRepository ratings = Substitute.For<IEventRatingRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly TestClock clock = new(today: new DateOnly(2026, 7, 10));
    private readonly SaveEventRatingCommandHandler sut;

    public SaveEventRatingCommandHandlerTests()
    {
        sut = new SaveEventRatingCommandHandler(
            events,
            ratings,
            activities,
            new FakeQueryExecutor(),
            clock
        );

        // Attendance defaults to none; individual tests seed a confirmed assignment when the
        // scenario requires one.
        activities
            .QueryAssignments()
            .Returns(Array.Empty<ActivityUserRoleAssignment>().AsQueryable());

        // The atomic repository write defaults to succeeding; individual tests override it to
        // exercise the already-submitted conflict path.
        ratings
            .SubmitAsync(Arg.Any<EventRating>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private static Event NewEvent(DateOnly endsAt)
    {
        return new Event
        {
            Id = EventId,
            Title = "Evento",
            Subtitle = "Sub",
            Description = "{}",
            EventStartsAt = endsAt.AddDays(-1),
            EventEndsAt = endsAt,
            SignupStartsAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            ThumbnailId = Guid.NewGuid(),
        };
    }

    private void SeedFinishedEvent()
    {
        events.Query().Returns(new[] { NewEvent(clock.Today.AddDays(-1)) }.AsQueryable());
    }

    private void SeedConfirmedAssignment(Guid attendeeUserId, Guid? parentId = null)
    {
        var attendee = new User
        {
            Id = attendeeUserId,
            ParentId = parentId,
            FirstName = "Nombre",
            LastName = "Apellido",
        };
        var activity = new Activity
        {
            Id = ActivityId,
            EventId = EventId,
            Title = "Actividad",
            Description = "Descripción",
            Location = "Sala",
        };
        activities
            .QueryAssignments()
            .Returns(
                new[]
                {
                    new ActivityUserRoleAssignment
                    {
                        ActivityId = ActivityId,
                        Activity = activity,
                        UserId = attendeeUserId,
                        User = attendee,
                        AssignmentStatusId = SeedIds.AssignmentStatusTypes.Confirmed,
                    },
                }.AsQueryable()
            );
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.Query().Returns(Array.Empty<Event>().AsQueryable());

        var result = await sut.HandleAsync(
            new SaveEventRatingCommand(EventId, UserId, ValidRequest),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task HandleAsyncEventNotFinishedReturnsConflict()
    {
        events.Query().Returns(new[] { NewEvent(clock.Today.AddDays(1)) }.AsQueryable());

        var result = await sut.HandleAsync(
            new SaveEventRatingCommand(EventId, UserId, ValidRequest),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventRatingNotFinished);
    }

    [Fact]
    public async Task HandleAsyncWithoutConfirmedAttendanceReturnsConflict()
    {
        SeedFinishedEvent();

        var result = await sut.HandleAsync(
            new SaveEventRatingCommand(EventId, UserId, ValidRequest),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventRatingAttendanceRequired);
        await ratings
            .DidNotReceiveWithAnyArgs()
            .SubmitAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAttendanceViaConfirmedChildSucceeds()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(ChildId, parentId: UserId);

        var result = await sut.HandleAsync(
            new SaveEventRatingCommand(EventId, UserId, ValidRequest),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await ratings
            .Received(1)
            .SubmitAsync(
                Arg.Is<EventRating>(r => r.EventId == EventId),
                UserId,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncAlreadySubmittedReturnsConflictWithoutPersisting()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);
        ratings
            .SubmitAsync(Arg.Any<EventRating>(), UserId, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.HandleAsync(
            new SaveEventRatingCommand(EventId, UserId, ValidRequest),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventRatingAlreadySubmitted);
    }

    [Fact]
    public async Task HandleAsyncValidSubmissionPersistsAnonymousRatingAndSubmission()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);

        var result = await sut.HandleAsync(
            new SaveEventRatingCommand(EventId, UserId, ValidRequest),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await ratings
            .Received(1)
            .SubmitAsync(
                Arg.Is<EventRating>(r =>
                    r.EventId == EventId && r.Score == 5 && r.MostLiked == "Bien"
                ),
                UserId,
                Arg.Any<CancellationToken>()
            );
    }
}
