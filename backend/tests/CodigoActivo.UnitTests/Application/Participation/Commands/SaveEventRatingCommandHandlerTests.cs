using AwesomeAssertions;
using CodigoActivo.API.Participation.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
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
    private readonly TestCurrentUser currentUser = new(
        global::CodigoActivo.Domain.Users.UserId.From(UserId)
    );
    private readonly TestClock clock = new(today: new DateOnly(2026, 7, 10));
    private readonly SaveEventRatingCommandHandler sut;

    public SaveEventRatingCommandHandlerTests()
    {
        sut = new SaveEventRatingCommandHandler(events, ratings, activities, currentUser, clock);

        // Attendance defaults to none; individual tests seed a confirmed assignment when the
        // scenario requires one.
        activities
            .HasConfirmedAttendanceAsync(
                Arg.Any<EventId>(),
                Arg.Any<UserId>(),
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
        events
            .GetByIdAsync(
                global::CodigoActivo.Domain.Events.EventId.From(EventId),
                Arg.Any<CancellationToken>()
            )
            .Returns(NewEvent(endsAt));
    }

    private void SeedFinishedEvent()
    {
        SeedEvent(clock.Today.AddDays(-1));
    }

    private void SeedConfirmedAssignment(Guid attendeeUserId, Guid? parentId = null)
    {
        activities
            .HasConfirmedAttendanceAsync(
                global::CodigoActivo.Domain.Events.EventId.From(EventId),
                global::CodigoActivo.Domain.Users.UserId.From(parentId ?? attendeeUserId),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.Finds(null);

        var result = await sut.HandleAsync(
            ValidRequest.ToCommand(global::CodigoActivo.Domain.Events.EventId.From(EventId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCode.EventNotFound);
    }

    [Fact]
    public async Task HandleAsyncEventNotFinishedReturnsConflict()
    {
        SeedEvent(clock.Today.AddDays(1));

        var result = await sut.HandleAsync(
            ValidRequest.ToCommand(global::CodigoActivo.Domain.Events.EventId.From(EventId)),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ApplicationErrorCode.EventRatingNotFinished);
    }

    [Fact]
    public async Task HandleAsyncWithoutConfirmedAttendanceReturnsConflict()
    {
        SeedFinishedEvent();

        var result = await sut.HandleAsync(
            ValidRequest.ToCommand(global::CodigoActivo.Domain.Events.EventId.From(EventId)),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ApplicationErrorCode.EventRatingAttendanceRequired);
        await ratings
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAttendanceViaConfirmedChildSucceeds()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(ChildId, parentId: UserId);

        var result = await sut.HandleAsync(
            ValidRequest.ToCommand(global::CodigoActivo.Domain.Events.EventId.From(EventId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await ratings
            .Received(1)
            .AddAsync(
                Arg.Is<EventRating>(r =>
                    r.EventId == global::CodigoActivo.Domain.Events.EventId.From(EventId)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncValidSubmissionAddsAnonymousRatingAndCommits()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);

        var result = await sut.HandleAsync(
            ValidRequest.ToCommand(global::CodigoActivo.Domain.Events.EventId.From(EventId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await ratings
            .Received(1)
            .AddAsync(
                Arg.Is<EventRating>(r =>
                    r.EventId == global::CodigoActivo.Domain.Events.EventId.From(EventId)
                    && r.Score == 5
                    && r.MostLiked == "Bien"
                    && r.LeastLiked == "La cola"
                    && r.Suggestions == "Más talleres"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncRepeatedSubmissionAddsAnotherRating()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);

        var first = await sut.HandleAsync(
            ValidRequest.ToCommand(global::CodigoActivo.Domain.Events.EventId.From(EventId)),
            TestContext.Current.CancellationToken
        );
        var second = await sut.HandleAsync(
            new SaveEventRatingRequest(1, "Otra cosa", null, null).ToCommand(
                global::CodigoActivo.Domain.Events.EventId.From(EventId)
            ),
            TestContext.Current.CancellationToken
        );

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        await ratings.Received(2).AddAsync(Arg.Any<EventRating>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEmptySubmissionReturnsValidationErrorWithoutSaving()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);

        var result = await sut.HandleAsync(
            new SaveEventRatingRequest(null, "  ", null, string.Empty).ToCommand(
                global::CodigoActivo.Domain.Events.EventId.From(EventId)
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.EventRatingEmpty);
        await ratings
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAnswersWithoutScoreAddsUnscoredRating()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);

        var result = await sut.HandleAsync(
            new SaveEventRatingRequest(null, null, null, "Más talleres").ToCommand(
                global::CodigoActivo.Domain.Events.EventId.From(EventId)
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
