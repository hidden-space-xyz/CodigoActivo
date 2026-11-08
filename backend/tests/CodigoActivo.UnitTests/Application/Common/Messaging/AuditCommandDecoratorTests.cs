using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Application.EventCategories.Commands;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
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
    private readonly CommandScope commandScope = new();
    private readonly TestCurrentUser currentUser = new();
    private readonly IRequestOrigin origin = Substitute.For<IRequestOrigin>();

    public AuditCommandDecoratorTests()
    {
        origin.ClientAddress.Returns(Address);
    }

    private Task<TResult> HandleAsync<TCommand, TResult>(
        TCommand command,
        Func<IAuditSubject, TResult> answer,
        RecordingLogger<AuditWriter>? logger = null
    )
        where TCommand : ICommand<TResult>
        where TResult : Result
    {
        var sut = new AuditCommandDecorator<TCommand, TResult>(
            new AnsweringHandler<TCommand, TResult>(() => answer(subject)),
            new AuditWriter(
                trail,
                currentUser,
                origin,
                logger ?? new RecordingLogger<AuditWriter>()
            ),
            subject,
            commandScope
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
    public async Task HandleAsyncSucceededCommandRecordsActorAddressAndEntity()
    {
        var target = UserId.New();

        var result = await HandleAsync<SetStaffRoleCommand, Result>(
            new SetStaffRoleCommand(
                target,
                StaffRole.Administrator,
                CurrentPassword: "secret-password"
            ),
            _ => Result.Success()
        );

        result.IsSuccess.Should().BeTrue();
        var record = trail.Records.Should().ContainSingle().Subject;
        record
            .Should()
            .BeEquivalentTo(
                new AuditRecord(
                    AuditAction.SetStaffRole,
                    AuditActorType.User,
                    currentUser.Id,
                    Address,
                    new AuditEntity(AuditEntityType.User, target.Value),
                    [],
                    AuditOutcome.Succeeded,
                    ErrorCode: null
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

        await HandleAsync<CompleteLoginCommand, Result<UserSessionId>>(
            new CompleteLoginCommand(
                new LoginChallengeKey(account, LoginChallengeId.New()),
                "123456"
            ),
            _ => UserSessionId.New()
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
                    new HouseholdMemberSignup(
                        first,
                        ActivityRole.Volunteer,
                        [new TermsDecision(document, Accepted: true, new byte[] { 1, 2, 3 })]
                    ),
                    new HouseholdMemberSignup(second, ActivityRole.Participant),
                ]
            ),
            _ => Result.Success<IReadOnlyList<UserId>>([first, second])
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record.Entity.Should().Be(new AuditEntity(AuditEntityType.Activity, activity.Value));
        record
            .Related.Should()
            .Equal(
                new AuditEntity(AuditEntityType.User, first.Value),
                new AuditEntity(AuditEntityType.TermsDocument, document.Value),
                new AuditEntity(AuditEntityType.User, second.Value)
            );
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
    public async Task HandleAsyncBlockedDataExtractNeverNamesItsAccount()
    {
        var consultation = BlockedDataConsultationId.New();
        var account = UserId.New();

        await HandleAsync<ExportBlockedDataCommand, Result<BlockedDataExport>>(
            new ExportBlockedDataCommand(consultation, account, null, null, []),
            _ => new BlockedDataExport(
                DateTimeOffset.UnixEpoch,
                "Juzgado de León",
                "Diligencias previas 123/2026",
                account.Value,
                null,
                null,
                [],
                [],
                null
            )
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record
            .Entity.Should()
            .Be(new AuditEntity(AuditEntityType.BlockedDataConsultation, consultation.Value));
        record.Related.Should().BeEmpty();
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
    public async Task HandleAsyncUniqueConstraintViolationIsRecordedAsRejectedAndRethrown()
    {
        var failure = new UniqueConstraintViolationException();

        var act = () =>
            FailAsync<DeleteUserCommand, Result>(new DeleteUserCommand(UserId.New()), failure);

        (await act.Should().ThrowAsync<UniqueConstraintViolationException>())
            .Which.Should()
            .BeSameAs(failure);
        var record = trail.Records.Should().ContainSingle().Subject;
        record.Outcome.Should().Be(AuditOutcome.Rejected);
        record.ErrorCode.Should().Be("UniqueConstraintViolation");
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
        var logger = new RecordingLogger<AuditWriter>();

        var result = await HandleAsync(new DeleteUserCommand(UserId.New()), _ => answer, logger);

        result.Should().BeSameAs(answer);
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("The audit entry of a DeleteUser could not be recorded");
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
    public async Task HandleAsyncAnyCommandNamesItselfInTheCommandScopeWhileItRuns()
    {
        string? audited = null;
        string? unaudited = null;

        await HandleAsync<PurgeDataHistoryCommand, Result<int>>(
            new PurgeDataHistoryCommand(),
            _ =>
            {
                audited = commandScope.Command;
                return 0;
            }
        );
        await HandleAsync<SaveEventRatingCommand, Result>(
            new SaveEventRatingCommand(EventId.New(), 5, null, null, null),
            _ =>
            {
                unaudited = commandScope.Command;
                return Result.Success();
            }
        );

        audited.Should().Be("PurgeDataHistory");
        unaudited.Should().Be("SaveEventRating");
        commandScope.Command.Should().BeNull();
    }

    [Fact]
    public void EnterNestedCommandGivesTheOuterNameBackWhenItEnds()
    {
        using (commandScope.Enter("Outer"))
        {
            using (commandScope.Enter("Inner"))
            {
                commandScope.Command.Should().Be("Inner");
            }

            commandScope.Command.Should().Be("Outer");
        }

        commandScope.Command.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncScheduledCommandIsRecordedOnlyWhenItChangedSomething()
    {
        currentUser.Id = null;

        await HandleAsync<PurgeDataHistoryCommand, Result<int>>(
            new PurgeDataHistoryCommand(),
            _ => 0
        );
        await HandleAsync<PurgeDataHistoryCommand, Result<int>>(
            new PurgeDataHistoryCommand(),
            _ => 3
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record.ActorType.Should().Be(AuditActorType.System);
        record.Entity.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncRoutineCommandIsRecordedWhenItSucceeds()
    {
        await HandleAsync<DeleteActivityCommand, Result>(
            new DeleteActivityCommand(ActivityId.New()),
            _ => Result.Success()
        );

        trail.Records.Should().ContainSingle().Which.Outcome.Should().Be(AuditOutcome.Succeeded);
    }

    [Fact]
    public async Task HandleAsyncRoutineCommandRefusedInEverydayUseIsNotRecorded()
    {
        await HandleAsync<DeleteActivityCommand, Result>(
            new DeleteActivityCommand(ActivityId.New()),
            _ => Error.NotFound(ApplicationErrorCode.ActivityNotFound)
        );
        var act = () =>
            FailAsync<DeleteActivityCommand, Result>(
                new DeleteActivityCommand(ActivityId.New()),
                new ConcurrentUpdateException()
            );
        await act.Should().ThrowAsync<ConcurrentUpdateException>();

        trail.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncRoutineCommandRefusedForLackOfPermissionIsRecorded()
    {
        await HandleAsync<DeleteActivityCommand, Result>(
            new DeleteActivityCommand(ActivityId.New()),
            _ => Error.Forbidden(ApplicationErrorCode.ActingForAnotherUserForbidden)
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record.Outcome.Should().Be(AuditOutcome.Rejected);
        record.ErrorCode.Should().Be(nameof(ApplicationErrorCode.ActingForAnotherUserForbidden));
    }

    [Fact]
    public async Task HandleAsyncRoutineCommandThatFailsIsRecorded()
    {
        var act = () =>
            FailAsync<DeleteActivityCommand, Result>(
                new DeleteActivityCommand(ActivityId.New()),
                new InvalidOperationException("boom")
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        trail.Records.Should().ContainSingle().Which.Outcome.Should().Be(AuditOutcome.Failed);
    }

    [Fact]
    public async Task HandleAsyncPreparatoryCommandIsRecordedOnlyWhenRefused()
    {
        await HandleAsync<RequestConfirmationCodeCommand, Result>(
            new RequestConfirmationCodeCommand(
                "right-password",
                ConfirmationCodePurpose.AccountDeletion
            ),
            _ => Result.Success()
        );
        await HandleAsync<RequestConfirmationCodeCommand, Result>(
            new RequestConfirmationCodeCommand(
                "wrong-password",
                ConfirmationCodePurpose.AccountDeletion
            ),
            _ => Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect)
        );

        var record = trail.Records.Should().ContainSingle().Subject;
        record.Outcome.Should().Be(AuditOutcome.Rejected);
        record.ErrorCode.Should().Be(nameof(ApplicationErrorCode.UserCurrentPasswordIncorrect));
        record.ToString().Should().NotContain("wrong-password");
    }

    [Fact]
    public async Task HandleAsyncAccountLookupIsRecordedOnlyWhenItFoundTheAccount()
    {
        currentUser.Id = null;
        var account = UserId.New();

        await HandleAsync<ForgotPasswordCommand, Result>(
            new ForgotPasswordCommand("nadie@test.com"),
            _ => Result.Success()
        );
        await HandleAsync<ForgotPasswordCommand, Result>(
            new ForgotPasswordCommand("ana@test.com"),
            named =>
            {
                named.Name(account);
                return Result.Success();
            }
        );

        trail
            .Records.Should()
            .ContainSingle()
            .Which.Entity.Should()
            .Be(new AuditEntity(AuditEntityType.User, account.Value));
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
