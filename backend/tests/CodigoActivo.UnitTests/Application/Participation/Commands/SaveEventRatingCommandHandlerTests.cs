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
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestCurrentUser currentUser = new(
        global::CodigoActivo.Domain.Users.UserId.From(UserId)
    );
    private readonly TestClock clock = new(today: new DateOnly(2026, 7, 10));
    private readonly SaveEventRatingCommandHandler sut;

    public SaveEventRatingCommandHandlerTests()
    {
        sut = new SaveEventRatingCommandHandler(
            events,
            ratings,
            activities,
            users,
            uow,
            currentUser,
            clock
        );
        users.ListDependentsAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns([]);
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

    private void SeedConfirmedAssignment(Guid attendeeUserId)
    {
        var attendee = global::CodigoActivo.Domain.Users.UserId.From(attendeeUserId);
        activities
            .AnyConfirmedSignupAsync(
                global::CodigoActivo.Domain.Events.EventId.From(EventId),
                Arg.Is<IReadOnlyCollection<UserId>>(ids => ids.Contains(attendee)),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
    }

    private void SeedDependent(Guid childId)
    {
        users
            .ListDependentsAsync(
                global::CodigoActivo.Domain.Users.UserId.From(UserId),
                Arg.Any<CancellationToken>()
            )
            .Returns([Persisted.As<User>(new { Id = childId, ParentId = UserId })]);
    }

    private EventRating SeedStoredRating()
    {
        var stored = EventRating
            .Submit(
                global::CodigoActivo.Domain.Events.EventId.From(EventId),
                global::CodigoActivo.Domain.Users.UserId.From(UserId),
                3,
                "Bien",
                "La cola",
                "Más talleres"
            )
            .Value;
        ratings
            .GetByAuthorAsync(
                global::CodigoActivo.Domain.Events.EventId.From(EventId),
                global::CodigoActivo.Domain.Users.UserId.From(UserId),
                Arg.Any<CancellationToken>()
            )
            .Returns(stored);
        return stored;
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
        SeedDependent(ChildId);
        SeedConfirmedAssignment(ChildId);

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
    public async Task HandleAsyncFirstSubmissionAddsTheRatingOfTheAccountAndCommits()
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
                    && r.UserId == global::CodigoActivo.Domain.Users.UserId.From(UserId)
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
    public async Task HandleAsyncStoredRatingIsRevisedWithoutAddingAnother()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);
        var stored = SeedStoredRating();

        var result = await sut.HandleAsync(
            new SaveEventRatingRequest(1, "Otra cosa", null, null).ToCommand(
                global::CodigoActivo.Domain.Events.EventId.From(EventId)
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        stored.Score.Should().Be(1);
        stored.MostLiked.Should().Be("Otra cosa");
        stored.LeastLiked.Should().BeNull();
        stored.Suggestions.Should().BeNull();
        await ratings.DidNotReceiveWithAnyArgs().AddAsync(default!, Arg.Any<CancellationToken>());
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEmptyRevisionReturnsValidationErrorAndKeepsTheRating()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);
        var stored = SeedStoredRating();

        var result = await sut.HandleAsync(
            new SaveEventRatingRequest(null, " ", null, null).ToCommand(
                global::CodigoActivo.Domain.Events.EventId.From(EventId)
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventRatingEmpty);
        stored.Score.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsyncConcurrentFirstRatingThrowsConcurrentUpdate()
    {
        SeedFinishedEvent();
        SeedConfirmedAssignment(UserId);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ =>
                throw new UniqueConstraintViolationException(
                    typeof(EventRating),
                    new InvalidOperationException()
                )
            );

        var act = () =>
            sut.HandleAsync(
                ValidRequest.ToCommand(global::CodigoActivo.Domain.Events.EventId.From(EventId)),
                TestContext.Current.CancellationToken
            );

        (await act.Should().ThrowAsync<ConcurrentUpdateException>())
            .Which.EntityType.Should()
            .Be<EventRating>();
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
