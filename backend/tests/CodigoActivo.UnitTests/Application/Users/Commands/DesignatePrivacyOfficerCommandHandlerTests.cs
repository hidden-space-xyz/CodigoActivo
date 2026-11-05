using AwesomeAssertions;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class DesignatePrivacyOfficerCommandHandlerTests
{
    private const string ActingPassword = "initial-admin-password";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly User initialAdmin;
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly CommittedEvents events;
    private readonly DesignatePrivacyOfficerCommandHandler sut;

    public DesignatePrivacyOfficerCommandHandlerTests()
    {
        initialAdmin = NewUser(
            id: KnownIds.Users.InitialAdministrator,
            email: "admin@test.com",
            isAdmin: true,
            typeId: KnownIds.UserTypes.Member,
            isPrivacyOfficer: true
        );
        Persisted.Overwrite(initialAdmin, new { PasswordHash = hasher.Hash(ActingPassword) });
        currentUser.Id = initialAdmin.Id;
        events = new CommittedEvents(
            new AccountSecurityNotifications(
                users,
                new AccountSecurityNotifier(
                    emailSender,
                    clock,
                    composer,
                    NullLogger<AccountSecurityNotifier>.Instance
                )
            )
        );
        sut = new DesignatePrivacyOfficerCommandHandler(
            users,
            currentUser,
            PasswordGuards.Create(hasher, uow, clock, users: users, currentUser: currentUser),
            uow.RunsTransactions(),
            clock
        );
    }

    private Task<Result> HandleAsync(Guid userId, string currentPassword = ActingPassword)
    {
        return sut.HandleAsync(
            new DesignatePrivacyOfficerRequest(currentPassword).ToCommand(UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    private ComposedAccountEmail Alert(User user, AccountSecurityChange change)
    {
        return new ComposedAccountEmail(
            nameof(IAccountEmailComposer.SecurityAlert),
            new EmailRecipient(user.Email!.Value, user.FirstName)
        )
        {
            Change = change,
            OccurredAt = clock.UtcNow,
        };
    }

    private void HolderIs(User? holder)
    {
        users.GetPrivacyOfficerAsync(Arg.Any<CancellationToken>()).Returns(holder);
    }

    [Fact]
    public async Task HandleAsyncAnotherAdministratorReturnsForbiddenWithoutCheckingThePassword()
    {
        var admin = NewUser(isAdmin: true);
        currentUser.Id = admin.Id;
        users.FindReturns(admin);

        var result = await HandleAsync(admin.Id.Value, "wrong-password");

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.SuperAdministratorOnly);
        admin.IsPrivacyOfficer.Should().BeFalse();
        await uow.DidNotReceiveWithAnyArgs()
            .ExecuteInTransactionAsync<bool>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncIncorrectPasswordCountsTheFailureAndDesignatesNobody()
    {
        var user = NewUser();
        users.FindReturns(initialAdmin, user);

        var result = await HandleAsync(user.Id.Value, "wrong-password");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        initialAdmin.PasswordLockout.FailedAttempts.Should().Be(1);
        user.IsPrivacyOfficer.Should().BeFalse();
        initialAdmin.IsPrivacyOfficer.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(initialAdmin, null);

        var result = await HandleAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        initialAdmin.IsPrivacyOfficer.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncAccountAlreadyDesignatedSucceedsWithoutMovingIt()
    {
        users.FindReturns(initialAdmin, initialAdmin);

        var result = await HandleAsync(initialAdmin.Id.Value);

        result.IsSuccess.Should().BeTrue();
        await users
            .DidNotReceiveWithAnyArgs()
            .GetPrivacyOfficerAsync(TestContext.Current.CancellationToken);
        await events.PublishAsync(initialAdmin);
        emailSender.Sent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, DomainErrorCode.UserDependentCannotHoldStaffRole)]
    [InlineData(false, DomainErrorCode.UserPendingVerificationCannotHoldStaffRole)]
    public async Task HandleAsyncIneligibleAccountReturnsTheRefusalAndKeepsTheHolder(
        bool dependent,
        DomainErrorCode expected
    )
    {
        var user = dependent
            ? NewUser(parentId: Guid.NewGuid(), statusId: KnownIds.UserStatusTypes.Dependent)
            : NewUser(statusId: KnownIds.UserStatusTypes.Pending);
        users.FindReturns(initialAdmin, user);

        var result = await HandleAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, expected);
        user.IsPrivacyOfficer.Should().BeFalse();
        initialAdmin.IsPrivacyOfficer.Should().BeTrue();
        await users
            .DidNotReceiveWithAnyArgs()
            .GetPrivacyOfficerAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncSavesTheReleaseBeforeTheDesignationAndTellsBothHolders()
    {
        var user = NewUser(email: "dpo@test.com", isAdmin: true, typeId: KnownIds.UserTypes.Member);
        users.FindReturns(initialAdmin, user);
        HolderIs(initialAdmin);
        var saved = new List<(bool Previous, bool Next)>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                saved.Add((initialAdmin.IsPrivacyOfficer, user.IsPrivacyOfficer));
                return 1;
            });

        var result = await HandleAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        saved.Should().Equal((false, false), (false, true));
        users.FindReturns(initialAdmin, user);
        await events.PublishAsync(initialAdmin, user);
        composer
            .Composed.Should()
            .BeEquivalentTo([
                Alert(initialAdmin, AccountSecurityChange.PrivacyOfficerReleased),
                Alert(user, AccountSecurityChange.PrivacyOfficerDesignated),
            ]);
    }

    [Fact]
    public async Task HandleAsyncInitialAdministratorTakesTheDesignationBack()
    {
        var holder = NewUser(email: "dpo@test.com", isPrivacyOfficer: true);
        Persisted.Overwrite(initialAdmin, new { IsPrivacyOfficer = false });
        users.FindReturns(initialAdmin, initialAdmin);
        HolderIs(holder);

        var result = await HandleAsync(initialAdmin.Id.Value);

        result.IsSuccess.Should().BeTrue();
        holder.IsPrivacyOfficer.Should().BeFalse();
        initialAdmin.IsPrivacyOfficer.Should().BeTrue();
        await uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncAccountThatIsNotAnAdministratorReturnsTheRefusalAndKeepsTheHolder()
    {
        var editor = NewUser(
            typeId: KnownIds.UserTypes.Member,
            staffRole: CodigoActivo.Application.Abstractions.Querying.ReadModel.StaffRole.Editor
        );
        users.FindReturns(initialAdmin, editor);

        var result = await HandleAsync(editor.Id.Value);

        result.ShouldFail(
            ErrorKind.Validation,
            DomainErrorCode.UserPrivacyOfficerMustBeAdministrator
        );
        editor.IsPrivacyOfficer.Should().BeFalse();
        initialAdmin.IsPrivacyOfficer.Should().BeTrue();
        await users
            .DidNotReceiveWithAnyArgs()
            .GetPrivacyOfficerAsync(TestContext.Current.CancellationToken);
    }
}
