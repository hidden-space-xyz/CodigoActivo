using AwesomeAssertions;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;
using StaffRoleContract = CodigoActivo.Application.Abstractions.Querying.ReadModel.StaffRole;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class SetStaffRoleCommandHandlerTests
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
    private readonly SetStaffRoleCommandHandler sut;

    public SetStaffRoleCommandHandlerTests()
    {
        initialAdmin = NewUser(
            id: KnownIds.Users.InitialAdministrator,
            isAdmin: true,
            typeId: KnownIds.UserTypes.Member
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
        sut = new SetStaffRoleCommandHandler(
            users,
            currentUser,
            PasswordGuards.Create(hasher, uow, clock, users: users, currentUser: currentUser),
            clock
        );
    }

    private Task<Result> HandleAsync(
        Guid userId,
        StaffRoleContract staffRole,
        string? currentPassword = null
    )
    {
        return sut.HandleAsync(
            new SetStaffRoleRequest(staffRole, currentPassword).ToCommand(UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotSaveAsync();
    }

    private ComposedAccountEmail RoleAlert(User user, AccountSecurityChange change)
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

    [Fact]
    public async Task HandleAsyncAnotherAdministratorReturnsForbiddenWithoutCheckingThePassword()
    {
        var admin = NewUser(isAdmin: true);
        Persisted.Overwrite(admin, new { PasswordHash = hasher.Hash(ActingPassword) });
        currentUser.Id = admin.Id;
        var user = NewUser();
        users.FindReturns(admin, user);

        var result = await HandleAsync(user.Id.Value, StaffRoleContract.Editor, "wrong");

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.SuperAdministratorOnly);
        user.StaffRole.Should().Be(StaffRole.None);
        admin.PasswordLockout.FailedAttempts.Should().Be(0);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(initialAdmin, null);

        var result = await HandleAsync(Guid.NewGuid(), StaffRoleContract.Editor, ActingPassword);

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData(StaffRoleContract.Administrator, ActingPassword)]
    [InlineData(StaffRoleContract.Editor, "wrong-password")]
    [InlineData(StaffRoleContract.None, null)]
    public async Task HandleAsyncOwnAccountReturnsForbiddenWithoutCheckingThePassword(
        StaffRoleContract staffRole,
        string? currentPassword
    )
    {
        users.FindReturns(initialAdmin);

        var result = await HandleAsync(initialAdmin.Id.Value, staffRole, currentPassword);

        result.ShouldFail(
            ErrorKind.Forbidden,
            ApplicationErrorCode.UserSelfStaffRoleChangeForbidden
        );
        initialAdmin.StaffRole.Should().Be(StaffRole.Administrator);
        initialAdmin.PasswordLockout.FailedAttempts.Should().Be(0);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
        await events.PublishAsync(initialAdmin);
        emailSender.Sent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(StaffRoleContract.Editor, StaffRole.Editor, AccountSecurityChange.EditorGranted)]
    [InlineData(
        StaffRoleContract.Administrator,
        StaffRole.Administrator,
        AccountSecurityChange.AdministratorGranted
    )]
    public async Task HandleAsyncGivingARoleWithCorrectPasswordGivesItAndTellsTheHolder(
        StaffRoleContract requested,
        StaffRole expected,
        AccountSecurityChange change
    )
    {
        var user = NewUser(typeId: KnownIds.UserTypes.Member);
        users.FindReturns(initialAdmin, user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await HandleAsync(user.Id.Value, requested, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        user.StaffRole.Should().Be(expected);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await events.PublishAsync(user);
        composer.Composed.Should().Equal(RoleAlert(user, change));
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be(user.Email!.Value);
    }

    [Fact]
    public async Task HandleAsyncDemotingAnAdministratorToEditorAsksForThePassword()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(initialAdmin, user);

        var result = await HandleAsync(user.Id.Value, StaffRoleContract.Editor);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.StaffRole.Should().Be(StaffRole.Administrator);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleAsyncGivingARoleWithoutPasswordReturnsBadRequest(
        string? currentPassword
    )
    {
        users.FindReturns(initialAdmin);

        var result = await HandleAsync(Guid.NewGuid(), StaffRoleContract.Editor, currentPassword);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGivingARoleWithIncorrectPasswordCountsTheFailure()
    {
        var user = NewUser();
        users.FindReturns(initialAdmin, user);

        var result = await HandleAsync(user.Id.Value, StaffRoleContract.Editor, "wrong-password");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.StaffRole.Should().Be(StaffRole.None);
        initialAdmin.PasswordLockout.FailedAttempts.Should().Be(1);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUnchangedRoleIsNoopAndTellsNobody()
    {
        var user = NewUser(staffRole: StaffRoleContract.Editor);
        users.FindReturns(initialAdmin, user);

        var result = await HandleAsync(user.Id.Value, StaffRoleContract.Editor, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        await AssertNotSavedAsync();
        await events.PublishAsync(user);
        emailSender.Sent.Should().BeEmpty("an unchanged role is not a security event to report");
    }

    [Theory]
    [InlineData(StaffRoleContract.Editor)]
    [InlineData(StaffRoleContract.Administrator)]
    public async Task HandleAsyncRevokingTakesTheRoleWithoutLoadingTheActingUser(
        StaffRoleContract current
    )
    {
        var user = NewUser(staffRole: current);
        users.FindReturns(user);

        var result = await HandleAsync(user.Id.Value, StaffRoleContract.None);

        result.IsSuccess.Should().BeTrue();
        user.StaffRole.Should().Be(StaffRole.None);
        await users.Received(1).GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
        await users.Received(1).GetByIdAsync(user.Id, Arg.Any<CancellationToken>());
        await events.PublishAsync(user);
        composer.Composed.Should().Equal(RoleAlert(user, AccountSecurityChange.StaffRoleRevoked));
    }

    [Fact]
    public async Task HandleAsyncGivingARoleToADependentReturnsTheDomainRefusal()
    {
        var dependent = NewUser(
            parentId: Guid.NewGuid(),
            statusId: KnownIds.UserStatusTypes.Dependent
        );
        users.FindReturns(initialAdmin, dependent);

        var result = await HandleAsync(
            dependent.Id.Value,
            StaffRoleContract.Editor,
            ActingPassword
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.UserDependentCannotHoldStaffRole);
        dependent.StaffRole.Should().Be(StaffRole.None);
    }

    [Fact]
    public async Task HandleAsyncGivingARoleToAnAccountThatIsNotAMemberReturnsTheDomainRefusal()
    {
        var user = NewUser(typeId: KnownIds.UserTypes.Participant);
        users.FindReturns(initialAdmin, user);

        var result = await HandleAsync(user.Id.Value, StaffRoleContract.Editor, ActingPassword);

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.UserStaffRoleRequiresMember);
        user.StaffRole.Should().Be(StaffRole.None);
    }

    [Fact]
    public async Task HandleAsyncDemotingThePrivacyOfficerReturnsTheDomainRefusal()
    {
        var officer = NewUser(
            isAdmin: true,
            typeId: KnownIds.UserTypes.Member,
            isPrivacyOfficer: true
        );
        users.FindReturns(officer);

        var result = await HandleAsync(officer.Id.Value, StaffRoleContract.None);

        result.ShouldFail(
            ErrorKind.Validation,
            DomainErrorCode.UserPrivacyOfficerMustBeAdministrator
        );
        officer.StaffRole.Should().Be(StaffRole.Administrator);
    }
}
