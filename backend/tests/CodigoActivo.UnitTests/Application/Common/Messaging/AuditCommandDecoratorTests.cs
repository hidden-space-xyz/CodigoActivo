using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Application.EventCategories.Commands;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Messaging;

public sealed class AuditCommandDecoratorTests
{
    private static readonly IPAddress Address = IPAddress.Parse("203.0.113.7");

    private readonly RecordingAuditTrail trail = new();
    private readonly AuditSubject subject = new();
    private readonly TestCurrentUser currentUser = new();
    private readonly IRequestOrigin origin = Substitute.For<IRequestOrigin>();

    public AuditCommandDecoratorTests()
    {
        origin.ClientAddress.Returns(Address);
    }

    private Task<TResult> HandleAsync<TCommand, TResult>(
        TCommand command,
        Func<IAuditSubject, TResult> answer,
        RecordingLogger<AuditCommandDecorator<TCommand, TResult>>? logger = null
    )
        where TCommand : ICommand<TResult>
        where TResult : Result
    {
        var sut = new AuditCommandDecorator<TCommand, TResult>(
            new AnsweringHandler<TCommand, TResult>(() => answer(subject)),
            trail,
            subject,
            currentUser,
            origin,
            logger ?? new RecordingLogger<AuditCommandDecorator<TCommand, TResult>>()
        );
        return sut.HandleAsync(command, TestContext.Current.CancellationToken);
    }

    private Task<TResult> FailAsync<TCommand, TResult>(TCommand command, Exception failure)
        where TCommand : ICommand<TResult>
        where TResult : Result
    {
        return HandleAsync<TCommand, TResult>(command, _ => throw failure);
    }

    [Fact]
    public async Task HandleAsyncSucceededCommandRecordsActorAddressEntityAndDetails()
    {
        var target = UserId.New();

        var result = await HandleAsync<SetAdminCommand, Result>(
            new SetAdminCommand(target, IsAdmin: true, CurrentPassword: "secret-password"),
            _ => Result.Success()
        );

        result.IsSuccess.Should().BeTrue();
        var record = trail.Records.Should().ContainSingle().Subject;
        record
            .Should()
            .BeEquivalentTo(
                new AuditRecord(
                    AuditAction.SetAdmin,
                    AuditActorType.User,
                    currentUser.Id,
                    Address,
                    new AuditEntity(AuditEntityType.User, target.Value),
                    [],
                    [new AuditDetail(nameof(SetAdminCommand.IsAdmin), true)],
                    AuditOutcome.Succeeded,
                    ErrorCode: null,
                    ItemCount: null
                )
            );
        record.ToString().Should().NotContain("secret-password");
    }

    [Fact]
    public async Task HandleAsyncAnonymousCallerRecordsTheAccountTheHandlerNamed()
    {
        currentUser.Id = null;
        var account = UserId.New();

        await HandleAsync<LoginCommand, Result<LoginChallengeKey>>(
            new LoginCommand("ana@test.com", "wrong-password"),
            named =>
            {
                named.Name(account);
                return Error.Unauthorized(ApplicationErrorCode.InvalidCredentials);
            }
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record.ActorType.Should().Be(AuditActorType.Anonymous);
        record.ActorId.Should().BeNull();
        record.Entity.Should().Be(new AuditEntity(AuditEntityType.User, account.Value));
        record.Outcome.Should().Be(AuditOutcome.Rejected);
        record.ErrorCode.Should().Be(nameof(ApplicationErrorCode.InvalidCredentials));
        record.ToString().Should().NotContain("ana@test.com").And.NotContain("wrong-password");
    }

    [Fact]
    public async Task HandleAsyncCreatingCommandRecordsTheCreatedEntity()
    {
        var created = EventCategoryTypeId.New();

        await HandleAsync<CreateEventCategoryTypeCommand, Result<EventCategoryTypeId>>(
            new CreateEventCategoryTypeCommand("Talleres", "#112233"),
            _ => created
        );

        trail
            .Records.Should()
            .ContainSingle()
            .Which.Entity.Should()
            .Be(new AuditEntity(AuditEntityType.EventCategoryType, created.Value));
    }

    [Fact]
    public async Task HandleAsyncLoginChallengeRecordsTheUserOfTheKeyAndNeverTheChallenge()
    {
        var account = UserId.New();

        await HandleAsync<ResendTwoFactorCodeCommand, Result>(
            new ResendTwoFactorCodeCommand(new LoginChallengeKey(account, LoginChallengeId.New())),
            _ => Result.Success()
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record.Entity.Should().Be(new AuditEntity(AuditEntityType.User, account.Value));
        record.Related.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncOtherIdentifiersBecomeRelatedListsAndRecordsIncluded()
    {
        var activity = ActivityId.New();
        var first = UserId.New();
        var second = UserId.New();
        var document = TermsDocumentId.New();

        await HandleAsync<AssignHouseholdCommand, Result<IReadOnlyList<UserId>>>(
            new AssignHouseholdCommand(
                activity,
                [
                    new HouseholdMemberSignup(first, ActivityRole.Volunteer),
                    new HouseholdMemberSignup(second, ActivityRole.Participant),
                ],
                [new TermsDecision(document, Accepted: true, new byte[] { 1, 2, 3 })]
            ),
            _ => Result.Success<IReadOnlyList<UserId>>([first, second])
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record.Entity.Should().Be(new AuditEntity(AuditEntityType.Activity, activity.Value));
        record
            .Related.Should()
            .Equal(
                new AuditEntity(AuditEntityType.User, first.Value),
                new AuditEntity(AuditEntityType.User, second.Value),
                new AuditEntity(AuditEntityType.TermsDocument, document.Value)
            );
        record.Details.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncIdentifiersNeverRecordedAreLeftOut()
    {
        var account = UserId.New();

        await HandleAsync<EndSessionCommand, Result>(
            new EndSessionCommand(account, UserSessionId.New()),
            _ => Result.Success()
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record.Entity.Should().Be(new AuditEntity(AuditEntityType.User, account.Value));
        record.Related.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncEnumChoicesAreRecordedByName()
    {
        await HandleAsync<ChangeAssignmentStatusCommand, Result>(
            new ChangeAssignmentStatusCommand(
                ActivityId.New(),
                UserId.New(),
                AssignmentStatus.Confirmed
            ),
            _ => Result.Success()
        );

        trail
            .Records.Should()
            .ContainSingle()
            .Which.Details.Should()
            .Equal(new AuditDetail(nameof(ChangeAssignmentStatusCommand.Status), "Confirmed"));
    }

    [Fact]
    public async Task HandleAsyncConcurrentUpdateIsRecordedAsRejectedAndRethrown()
    {
        var failure = new ConcurrentUpdateException();

        var act = () =>
            FailAsync<DeleteUserCommand, Result>(new DeleteUserCommand(UserId.New()), failure);

        (await act.Should().ThrowAsync<ConcurrentUpdateException>())
            .Which.Should()
            .BeSameAs(failure);
        var record = trail.Records.Should().ContainSingle().Subject;
        record.Outcome.Should().Be(AuditOutcome.Rejected);
        record.ErrorCode.Should().Be("ConcurrentUpdate");
    }

    [Fact]
    public async Task HandleAsyncUnexpectedExceptionIsRecordedAsFailedAndRethrown()
    {
        var act = () =>
            FailAsync<DeleteUserCommand, Result>(
                new DeleteUserCommand(UserId.New()),
                new InvalidOperationException("boom")
            );

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        var record = trail.Records.Should().ContainSingle().Subject;
        record.Outcome.Should().Be(AuditOutcome.Failed);
        record.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncTrailFailureLeavesTheResultUntouched()
    {
        trail.Failure = new InvalidOperationException("queue gone");
        var answer = Result.Success();
        var logger = new RecordingLogger<AuditCommandDecorator<DeleteUserCommand, Result>>();

        var result = await HandleAsync(new DeleteUserCommand(UserId.New()), _ => answer, logger);

        result.Should().BeSameAs(answer);
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("The audit entry of a DeleteUserCommand could not be recorded");
    }

    [Fact]
    public async Task HandleAsyncUnauditedCommandIsNeverRecorded()
    {
        await HandleAsync<SaveEventRatingCommand, Result>(
            new SaveEventRatingCommand(EventId.New(), 5, null, null, null),
            _ => Result.Success()
        );

        trail.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncScheduledCommandIsRecordedOnlyWhenItChangedSomething()
    {
        currentUser.Id = null;

        await HandleAsync<PurgeDeletedAccountsCommand, Result<int>>(
            new PurgeDeletedAccountsCommand(),
            _ => 0
        );
        await HandleAsync<PurgeDeletedAccountsCommand, Result<int>>(
            new PurgeDeletedAccountsCommand(),
            _ => 3
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record.ActorType.Should().Be(AuditActorType.System);
        record.ItemCount.Should().Be(3);
        record.Entity.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncSubjectOfAnEarlierCommandIsForgotten()
    {
        subject.Name(UserId.New());
        var target = UserId.New();

        await HandleAsync<DeleteUserCommand, Result>(
            new DeleteUserCommand(target),
            _ => Result.Success()
        );

        trail
            .Records.Should()
            .ContainSingle()
            .Which.Entity.Should()
            .Be(new AuditEntity(AuditEntityType.User, target.Value));
        subject.Entity.Should().BeNull("the next command of the scope starts clean");
    }

    private sealed class AnsweringHandler<TCommand, TResult>(Func<TResult> answer)
        : ICommandHandler<TCommand, TResult>
        where TCommand : ICommand<TResult>
        where TResult : Result
    {
        public Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default)
        {
            return Task.FromResult(answer());
        }
    }
}
