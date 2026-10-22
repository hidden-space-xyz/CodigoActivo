using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.Domain.Users.Erasure;
using CodigoActivo.UnitTests.Application.Activities;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Activities;

public sealed class DeletedParticipationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly UserId actorId = UserId.New();

    [Fact]
    public void OfEventScopesTheWholeEvent()
    {
        var eventId = EventId.New();

        ParticipationDeletion
            .OfEvent(eventId, actorId, Now)
            .Should()
            .Be(
                new ParticipationDeletion(
                    ParticipationDeletionOrigin.EventDeleted,
                    eventId,
                    null,
                    null,
                    actorId,
                    Now
                )
            );
    }

    [Fact]
    public void OfActivityAndOfSignupScopeTheActivityOfItsEventAndThePerson()
    {
        var activity = ActivityTestData.NewActivity();
        var personId = UserId.New();

        ParticipationDeletion
            .OfActivity(activity, actorId, Now)
            .Should()
            .Be(
                new ParticipationDeletion(
                    ParticipationDeletionOrigin.ActivityDeleted,
                    activity.EventId,
                    activity.Id,
                    null,
                    actorId,
                    Now
                )
            );
        ParticipationDeletion
            .OfSignup(activity, personId, actorId, Now)
            .Should()
            .Be(
                new ParticipationDeletion(
                    ParticipationDeletionOrigin.SignupCancelled,
                    activity.EventId,
                    activity.Id,
                    personId,
                    actorId,
                    Now
                )
            );
    }

    [Fact]
    public void RecordKeepsTheCopyWithTheMomentOfTheRemovalUnderANewId()
    {
        var deletion = ParticipationDeletion.OfEvent(EventId.New(), actorId, Now);

        var first = DeletedParticipation.Record(deletion, "{\"event\":{}}");
        var second = DeletedParticipation.Record(deletion, "{\"event\":{}}");

        first.DeletedAt.Should().Be(Now);
        first.Data.Should().Be("{\"event\":{}}");
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void PurgeCutoffIsThreeYearsBeforeNowLikeForAccounts()
    {
        DeletedParticipation.PurgeCutoff(Now).Should().Be(DeletedAccount.PurgeCutoff(Now));
        DeletedParticipation.PurgeCutoff(Now).Should().Be(Now.AddYears(-3));
    }
}
