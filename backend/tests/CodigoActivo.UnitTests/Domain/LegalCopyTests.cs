using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class LegalCopyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Created = new(2025, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FirstDay = new(2026, 8, 1);
    private static readonly Guid AccountId = new("10000000-0000-0000-0000-000000000000");
    private static readonly Guid GuardianId = new("20000000-0000-0000-0000-000000000000");
    private static readonly AccountErasure Erasure = new(
        AccountDeletionOrigin.Guardian,
        UserId.From(GuardianId),
        Now
    );

    private static Guid Id(int value)
    {
        return new Guid($"00000000-0000-0000-0000-{value:x12}");
    }

    private static LegalCopyPerson Person(
        Guid id,
        string firstName,
        string lastName,
        Guid? parentId = null
    )
    {
        return new LegalCopyPerson(
            id,
            firstName,
            lastName,
            null,
            null,
            null,
            null,
            null,
            Gender.Other,
            false,
            KnownIds.UserTypes.Participant,
            "Participante",
            KnownIds.UserStatusTypes.Active,
            "Activa",
            false,
            TwoFactorMethod.Email,
            parentId,
            Created,
            null,
            null
        );
    }

    private static LegalCopyEvent Event(Guid id, string title, DateOnly startsOn)
    {
        return new LegalCopyEvent(id, title, startsOn, startsOn.AddDays(1), [], []);
    }

    private static LegalCopyTermsDocument Document(Guid id, string name)
    {
        return new LegalCopyTermsDocument(id, name, "{}");
    }

    private static LegalCopyDecision Decision(
        LegalCopyTermsDocument document,
        DateTimeOffset decidedAt,
        Guid decidedBy
    )
    {
        return new LegalCopyDecision(document.Id, document.Name, true, decidedAt, decidedBy);
    }

    private static LegalCopyActivity Activity(
        Guid id,
        string title,
        DateTimeOffset startsAt,
        Guid participantId
    )
    {
        return new LegalCopyActivity(
            id,
            title,
            "Sala A",
            "Presencial",
            startsAt,
            startsAt.AddHours(2),
            participantId,
            "Participante",
            "Confirmada",
            Created
        );
    }

    private static LegalCopy Compose(
        IReadOnlyCollection<LegalCopyPerson> household,
        IReadOnlyCollection<(Guid EventId, LegalCopyActivity Activity)>? signups = null,
        IReadOnlyCollection<(Guid EventId, LegalCopyDecision Decision)>? decisions = null,
        IReadOnlyCollection<LegalCopyTermsDocument>? documents = null,
        IReadOnlyCollection<LegalCopyEvent>? events = null,
        LegalCopyGuardian? guardian = null
    )
    {
        return LegalCopy.Compose(
            AccountId,
            Erasure,
            household,
            guardian,
            signups ?? [],
            decisions ?? [],
            documents ?? [],
            events ?? []
        );
    }

    [Fact]
    public void ComposeKeepsWhoErasedTheAccountTheAccountAndItsGuardian()
    {
        var account = Person(AccountId, "Leo", "Ruiz", GuardianId);
        var guardian = new LegalCopyGuardian(
            GuardianId,
            "Ana",
            "Ruiz",
            "12345678Z",
            "ana@test.com",
            "600111222"
        );

        var copy = Compose([account], guardian: guardian);

        copy.Deletion.Should()
            .Be(new LegalCopyDeletion(AccountDeletionOrigin.Guardian, GuardianId));
        copy.Account.Should().BeSameAs(account);
        copy.Guardian.Should().BeSameAs(guardian);
        copy.Dependents.Should().BeEmpty();
        copy.TermsDocuments.Should().BeEmpty();
        copy.Events.Should().BeEmpty();
    }

    [Fact]
    public void ComposeDependentsAreSortedByFirstNameLastNameAndIdLeavingTheAccountOut()
    {
        var account = Person(AccountId, "Ana", "Ruiz");
        var zoe = Person(Id(1), "Zoe", "Ruiz", AccountId);
        var leoSanz = Person(Id(2), "Leo", "Sanz", AccountId);
        var secondLeoMora = Person(Id(4), "Leo", "Mora", AccountId);
        var firstLeoMora = Person(Id(3), "Leo", "Mora", AccountId);

        var copy = Compose([zoe, account, leoSanz, secondLeoMora, firstLeoMora]);

        copy.Account.Should().BeSameAs(account);
        copy.Dependents.Should().Equal(firstLeoMora, secondLeoMora, leoSanz, zoe);
    }

    [Fact]
    public void ComposeKeepsEveryHouseholdDecisionEvenWithoutASignup()
    {
        var account = Person(AccountId, "Ana", "Ruiz");
        var child = Person(Id(1), "Leo", "Ruiz", AccountId);
        var fair = Event(Id(10), "Feria", FirstDay);
        var consent = Document(Id(20), "Consentimiento");
        var byAccount = Decision(consent, Now, AccountId);
        var byChild = Decision(consent, Now.AddMinutes(1), child.Id);

        var copy = Compose(
            [account, child],
            decisions: [(fair.Id, byChild), (fair.Id, byAccount)],
            documents: [consent],
            events: [fair]
        );

        var ev = copy.Events.Should().ContainSingle().Subject;
        ev.Id.Should().Be(fair.Id);
        ev.TermsDecisions.Should().Equal(byAccount, byChild);
        ev.Activities.Should().BeEmpty();
        copy.TermsDocuments.Should().Equal(consent);
    }

    [Fact]
    public void ComposeKeepsGuardianDecisionsOnlyForEventsTheHouseholdSignedUpTo()
    {
        var account = Person(AccountId, "Leo", "Ruiz", GuardianId);
        var signedUp = Event(Id(10), "Feria", FirstDay);
        var notSignedUp = Event(Id(11), "Congreso", FirstDay.AddMonths(1));
        var authorization = Document(Id(20), "Autorización");
        var imageRights = Document(Id(21), "Derechos de imagen");
        var kept = Decision(authorization, Now, GuardianId);
        var dropped = Decision(imageRights, Now, GuardianId);
        var byStranger = Decision(authorization, Now.AddMinutes(1), Id(99));
        var signup = Activity(Id(30), "Taller", Start, AccountId);

        var copy = Compose(
            [account],
            signups: [(signedUp.Id, signup)],
            decisions: [(signedUp.Id, kept), (notSignedUp.Id, dropped), (signedUp.Id, byStranger)],
            documents: [authorization, imageRights],
            events: [signedUp, notSignedUp]
        );

        var ev = copy.Events.Should().ContainSingle().Subject;
        ev.Id.Should().Be(signedUp.Id);
        ev.TermsDecisions.Should().Equal(kept);
        ev.Activities.Should().Equal(signup);
        copy.TermsDocuments.Should().Equal(authorization);
    }

    [Fact]
    public void ComposeKeepsOnlyTheDocumentsAndEventsTheCopyRefersToSortedByNameAndId()
    {
        var account = Person(AccountId, "Ana", "Ruiz");
        var fair = Event(Id(10), "Feria", FirstDay);
        var unrelated = Event(Id(11), "Congreso", FirstDay);
        var privacy = Document(Id(21), "Privacidad");
        var secondImage = Document(Id(23), "Imagen");
        var firstImage = Document(Id(22), "Imagen");
        var unused = Document(Id(24), "Autorización");

        var copy = Compose(
            [account],
            decisions:
            [
                (fair.Id, Decision(privacy, Now, AccountId)),
                (fair.Id, Decision(secondImage, Now, AccountId)),
                (fair.Id, Decision(firstImage, Now, AccountId)),
            ],
            documents: [privacy, unused, secondImage, firstImage],
            events: [unrelated, fair]
        );

        copy.TermsDocuments.Should().Equal(firstImage, secondImage, privacy);
        copy.Events.Select(ev => ev.Id).Should().Equal(fair.Id);
    }

    [Fact]
    public void ComposeEventsAreSortedByFirstDayTitleAndId()
    {
        var account = Person(AccountId, "Ana", "Ruiz");
        var later = Event(Id(1), "A", FirstDay.AddMonths(1));
        var titledB = Event(Id(2), "B", FirstDay);
        var secondTitledA = Event(Id(4), "A", FirstDay);
        var firstTitledA = Event(Id(3), "A", FirstDay);

        var copy = Compose(
            [account],
            signups:
            [
                (later.Id, Activity(Id(31), "Taller", Start, AccountId)),
                (titledB.Id, Activity(Id(32), "Taller", Start, AccountId)),
                (secondTitledA.Id, Activity(Id(33), "Taller", Start, AccountId)),
                (firstTitledA.Id, Activity(Id(34), "Taller", Start, AccountId)),
            ],
            events: [later, titledB, secondTitledA, firstTitledA]
        );

        copy.Events.Select(ev => ev.Id)
            .Should()
            .Equal(firstTitledA.Id, secondTitledA.Id, titledB.Id, later.Id);
    }

    [Fact]
    public void ComposeDecisionsAreSortedByDateDocumentNameAndDocumentId()
    {
        var account = Person(AccountId, "Ana", "Ruiz");
        var fair = Event(Id(10), "Feria", FirstDay);
        var later = Decision(Document(Id(21), "A"), Now.AddHours(1), AccountId);
        var namedB = Decision(Document(Id(22), "B"), Now, AccountId);
        var secondNamedA = Decision(Document(Id(24), "A"), Now, AccountId);
        var firstNamedA = Decision(Document(Id(23), "A"), Now, AccountId);

        var copy = Compose(
            [account],
            decisions:
            [
                (fair.Id, later),
                (fair.Id, namedB),
                (fair.Id, secondNamedA),
                (fair.Id, firstNamedA),
            ],
            events: [fair]
        );

        copy.Events.Should()
            .ContainSingle()
            .Which.TermsDecisions.Should()
            .Equal(firstNamedA, secondNamedA, namedB, later);
    }

    [Fact]
    public void ComposeActivitiesAreSortedByStartTitleIdAndParticipant()
    {
        var account = Person(AccountId, "Ana", "Ruiz");
        var child = Person(Id(1), "Leo", "Ruiz", AccountId);
        var fair = Event(Id(10), "Feria", FirstDay);
        var later = Activity(Id(31), "A", Start.AddHours(2), AccountId);
        var titledB = Activity(Id(32), "B", Start, AccountId);
        var secondId = Activity(Id(34), "A", Start, AccountId);
        var firstIdByAccount = Activity(Id(33), "A", Start, AccountId);
        var firstIdByChild = Activity(Id(33), "A", Start, child.Id);

        var copy = Compose(
            [account, child],
            signups:
            [
                (fair.Id, later),
                (fair.Id, titledB),
                (fair.Id, secondId),
                (fair.Id, firstIdByAccount),
                (fair.Id, firstIdByChild),
            ],
            events: [fair]
        );

        copy.Events.Should()
            .ContainSingle()
            .Which.Activities.Should()
            .Equal(firstIdByChild, firstIdByAccount, secondId, titledB, later);
    }

    [Fact]
    public void ComposeFilesEveryDecisionAndSignupUnderItsOwnEvent()
    {
        var account = Person(AccountId, "Ana", "Ruiz");
        var fair = Event(Id(10), "Feria", FirstDay);
        var congress = Event(Id(11), "Congreso", FirstDay.AddMonths(1));
        var terms = Document(Id(20), "Autorización");
        var fairDecision = Decision(terms, Now, AccountId);
        var congressDecision = Decision(terms, Now.AddDays(1), AccountId);
        var fairSignup = Activity(Id(30), "Taller", Start, AccountId);
        var congressSignup = Activity(Id(31), "Charla", Start.AddMonths(1), AccountId);

        var copy = Compose(
            [account],
            signups: [(congress.Id, congressSignup), (fair.Id, fairSignup)],
            decisions: [(congress.Id, congressDecision), (fair.Id, fairDecision)],
            documents: [terms],
            events: [congress, fair]
        );

        copy.Events.Should().HaveCount(2);
        copy.Events[0].Id.Should().Be(fair.Id);
        copy.Events[0].TermsDecisions.Should().Equal(fairDecision);
        copy.Events[0].Activities.Should().Equal(fairSignup);
        copy.Events[1].Id.Should().Be(congress.Id);
        copy.Events[1].TermsDecisions.Should().Equal(congressDecision);
        copy.Events[1].Activities.Should().Equal(congressSignup);
        copy.TermsDocuments.Should().Equal(terms);
    }
}
