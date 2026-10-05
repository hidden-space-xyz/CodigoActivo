using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Participation.Commands;

public sealed class SaveEventRatingCommandHandlerTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChildId = Guid.NewGuid();

    private static readonly SaveEventRatingRequest ValidRequest = new(
        5,
        "Bien",
        "La cola",
        "Más talleres"
    );

    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IEventRatingRepository ratings = Substitute.For<IEventRatingRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new(today: new DateOnly(2026, 7, 10));
    private readonly SaveEventRatingCommandHandler sut;

    public SaveEventRatingCommandHandlerTests()
    {
        sut = new SaveEventRatingCommandHandler(events, ratings, activities, clock, uow);

        // Attendance defaults to none; individual tests seed a confirmed assignment when the
        // scenario requires one.
        activities
            .HasConfirmedAttendanceAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);
    }

    private static Event NewEvent(DateOnly endsAt)
    {
        return Persisted.As<Event>(
            new
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
            }
        );
    }

    private void SeedEvent(DateOnly endsAt)
    {
        events.GetByIdAsync(EventId, Arg.Any<CancellationToken>()).Returns(NewEvent(endsAt));
    }

    private void SeedFinishedEvent()
    {
        SeedEvent(clock.Today.AddDays(-1));
    }

    private void SeedConfirmedAssignment(Guid attendeeUserId, Guid? parentId = null)
    {
        activities
            .HasConfirmedAttendanceAsync(
                EventId,
                parentId ?? attendeeUserId,
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.Finds(null);

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
        SeedEvent(clock.Today.AddDays(1));

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
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
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
            .AddAsync(Arg.Is<EventRating>(r => r.EventId == EventId), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncValidSubmissionAddsAnonymousRatingAndCommits()
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
            .AddAsync(
                Arg.Is<EventRating>(r =>
                    r.EventId == EventId
                    && r.Score == 5
                    && r.MostLiked == "Bien"
                    && r.LeastLiked == "La cola"
                    && r.Suggestions == "Más talleres"
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncRepeatedSubmissionAddsAnotherRating()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);

        var first = await sut.HandleAsync(
            new SaveEventRatingCommand(EventId, UserId, ValidRequest),
            TestContext.Current.CancellationToken
        );
        var second = await sut.HandleAsync(
            new SaveEventRatingCommand(
                EventId,
                UserId,
                new SaveEventRatingRequest(1, "Otra cosa", null, null)
            ),
            TestContext.Current.CancellationToken
        );

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        await ratings.Received(2).AddAsync(Arg.Any<EventRating>(), Arg.Any<CancellationToken>());
        await uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEmptySubmissionReturnsValidationErrorWithoutSaving()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);

        var result = await sut.HandleAsync(
            new SaveEventRatingCommand(
                EventId,
                UserId,
                new SaveEventRatingRequest(null, "  ", null, string.Empty)
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.EventRatingEmpty);
        await ratings
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAnswersWithoutScoreAddsUnscoredRating()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);

        var result = await sut.HandleAsync(
            new SaveEventRatingCommand(
                EventId,
                UserId,
                new SaveEventRatingRequest(null, null, null, "Más talleres")
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await ratings
            .Received(1)
            .AddAsync(
                Arg.Is<EventRating>(r => r.Score == null && r.Suggestions == "Más talleres"),
                Arg.Any<CancellationToken>()
            );
    }
}
