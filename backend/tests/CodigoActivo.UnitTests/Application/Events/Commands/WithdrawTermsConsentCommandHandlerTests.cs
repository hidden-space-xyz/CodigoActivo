using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class WithdrawTermsConsentCommandHandlerTests
{
    private static readonly DateTimeOffset DecidedAt = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly IEventTermsAcceptanceRepository termsAcceptances =
        Substitute.For<IEventTermsAcceptanceRepository>();
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakeReadStore store = new();
    private readonly TestClock clock = new();
    private readonly EventId eventId = EventId.New();
    private readonly UserId guardianId = UserId.New();
    private readonly TermsDocument consent = Document(TermsDocumentKind.Consent);

    private static TermsDocument Document(TermsDocumentKind kind)
    {
        return TermsDocument.Create("Imagen", RichText.From("{}"), false, kind);
    }

    private WithdrawTermsConsentCommandHandler Handler(TestCurrentUser currentUser)
    {
        return new WithdrawTermsConsentCommandHandler(
            termsAcceptances,
            termsDocuments,
            users,
            ActingUsers.Policy(currentUser, store),
            currentUser,
            clock
        );
    }

    private User Dependent(int age)
    {
        var dependent = Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                ParentId = guardianId.Value,
                BirthDate = clock.Today.AddYears(-age),
            }
        );
        store.AddDependent(dependent.Id.Value, guardianId.Value);
        users.Finds(dependent);
        return dependent;
    }

    private EventTermsAcceptance Decided(User person, bool accepted = true)
    {
        var decision = EventTermsAcceptance.Record(
            eventId,
            person.Id,
            guardianId,
            consent.Id,
            accepted,
            null,
            guardianId,
            DecidedAt
        );
        termsAcceptances
            .GetAsync(eventId, person.Id, consent.Id, Arg.Any<CancellationToken>())
            .Returns(decision);
        return decision;
    }

    private Task<Result> WithdrawAsync(TestCurrentUser currentUser, User person)
    {
        return Handler(currentUser)
            .HandleAsync(
                new WithdrawTermsConsentCommand(eventId, person.Id, consent.Id),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task HandleAsyncGuardianWithdrawsTheConsentOfAChildUnder14()
    {
        termsDocuments.Finds(consent);
        var child = Dependent(age: 10);
        var decision = Decided(child);

        var result = await WithdrawAsync(new TestCurrentUser(guardianId), child);

        result.IsSuccess.Should().BeTrue();
        decision.Accepted.Should().BeFalse();
        var withdrawal = decision.Changes.Last();
        withdrawal.Accepted.Should().BeFalse();
        withdrawal.DecidedById.Should().Be(guardianId);
        withdrawal.RecordedById.Should().Be(guardianId);
        withdrawal.ChangedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncWithdrawalOfADependentFrom14IsTheirsEvenWhenTheGuardianRecordsIt()
    {
        termsDocuments.Finds(consent);
        var teenager = Dependent(age: 15);
        var decision = Decided(teenager);

        var result = await WithdrawAsync(new TestCurrentUser(guardianId), teenager);

        result.IsSuccess.Should().BeTrue();
        decision.Changes.Last().DecidedById.Should().Be(teenager.Id);
        decision.Changes.Last().RecordedById.Should().Be(guardianId);
    }

    [Fact]
    public async Task HandleAsyncAdministratorRecordsAWithdrawalAskedForByEmail()
    {
        termsDocuments.Finds(consent);
        var child = Dependent(age: 10);
        var decision = Decided(child);
        var admin = new TestCurrentUser(isAdmin: true);

        var result = await WithdrawAsync(admin, child);

        result.IsSuccess.Should().BeTrue();
        decision.Changes.Last().DecidedById.Should().Be(guardianId);
        decision.Changes.Last().RecordedById.Should().Be(admin.Id!.Value);
        decision
            .PullDomainEvents()
            .Should()
            .Equal(new TermsConsentWithdrawn(eventId, child.Id, consent.Id, admin.Id!.Value));
    }

    [Fact]
    public async Task HandleAsyncSomeoneElseIsForbidden()
    {
        termsDocuments.Finds(consent);
        var child = Dependent(age: 10);
        var decision = Decided(child);

        var result = await WithdrawAsync(new TestCurrentUser(), child);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.ActingForAnotherUserForbidden);
        decision.Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncConditionsCannotBeWithdrawn()
    {
        termsDocuments.Finds(Document(TermsDocumentKind.Conditions));
        var child = Dependent(age: 10);
        var decision = Decided(child);

        var result = await WithdrawAsync(new TestCurrentUser(guardianId), child);

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventTermsWithdrawalNotAllowed);
        decision.Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncConsentNotGivenIsAConflict()
    {
        termsDocuments.Finds(consent);
        var child = Dependent(age: 10);
        Decided(child, accepted: false);

        var result = await WithdrawAsync(new TestCurrentUser(guardianId), child);

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.EventTermsConsentNotGiven);
    }

    [Fact]
    public async Task HandleAsyncNoDecisionReturnsNotFound()
    {
        termsDocuments.Finds(consent);
        var child = Dependent(age: 10);

        var result = await WithdrawAsync(new TestCurrentUser(guardianId), child);

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.EventTermsDecisionNotFound);
    }

    [Fact]
    public async Task HandleAsyncDocumentMissingReturnsNotFound()
    {
        termsDocuments.Finds(null);
        var child = Dependent(age: 10);
        Decided(child);

        var result = await WithdrawAsync(new TestCurrentUser(guardianId), child);

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.TermsDocumentNotFound);
    }

    [Fact]
    public async Task HandleAsyncPersonMissingReturnsNotFound()
    {
        termsDocuments.Finds(consent);
        users.Finds(null);
        var admin = new TestCurrentUser(isAdmin: true);

        var result = await Handler(admin)
            .HandleAsync(
                new WithdrawTermsConsentCommand(eventId, UserId.New(), consent.Id),
                TestContext.Current.CancellationToken
            );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }
}
