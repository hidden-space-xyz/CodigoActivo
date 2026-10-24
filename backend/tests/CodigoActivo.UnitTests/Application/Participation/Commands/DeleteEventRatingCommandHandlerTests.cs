using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Participation.Commands;

public sealed class DeleteEventRatingCommandHandlerTests
{
    private static readonly EventId RatedEventId = EventId.New();
    private static readonly UserId AuthorId = UserId.New();

    private readonly IEventRatingRepository ratings = Substitute.For<IEventRatingRepository>();
    private readonly TestCurrentUser currentUser = new(AuthorId);
    private readonly TestClock clock = new();
    private readonly IParticipationArchive archive = Substitute.For<IParticipationArchive>();
    private readonly IDeletedParticipationRepository deletedParticipations =
        Substitute.For<IDeletedParticipationRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly DeleteEventRatingCommandHandler sut;

    public DeleteEventRatingCommandHandlerTests()
    {
        sut = new DeleteEventRatingCommandHandler(
            ratings,
            currentUser,
            ParticipationErasers.Create(archive, deletedParticipations, uow),
            clock
        );
    }

    [Fact]
    public async Task HandleAsyncWithoutRatingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new DeleteEventRatingCommand(RatedEventId),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.EventRatingNotFound);
        await archive.DidNotReceiveWithAnyArgs().LockAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncOwnRatingIsRemovedWithItsBlockedCopy()
    {
        var rating = EventRating.Submit(RatedEventId, AuthorId, 4, "Bien", null, null).Value;
        ratings
            .GetByAuthorAsync(RatedEventId, AuthorId, Arg.Any<CancellationToken>())
            .Returns(rating);

        var result = await sut.HandleAsync(
            new DeleteEventRatingCommand(RatedEventId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        ratings.Received(1).Remove(rating);
        var deletion = ParticipationDeletion.OfRating(rating, clock.UtcNow);
        deletion.Origin.Should().Be(ParticipationDeletionOrigin.RatingDeleted);
        deletion.PersonId.Should().Be(AuthorId);
        deletion.ActorId.Should().Be(AuthorId);
        deletion.ActivityId.Should().BeNull();
        await archive.Received(1).LockAsync(deletion, Arg.Any<CancellationToken>());
        await deletedParticipations
            .Received(1)
            .AddAsync(
                Arg.Is<DeletedParticipation>(copy => copy.Data == ParticipationErasers.Copy),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
