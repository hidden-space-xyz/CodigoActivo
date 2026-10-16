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

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class SetAdminCommandHandlerTests
{
    private const string ActingPassword = "acting-admin-password";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly User actingAdmin;
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly CommittedEvents events;
    private readonly SetAdminCommandHandler sut;

    public SetAdminCommandHandlerTests()
    {
        actingAdmin = NewUser(isAdmin: true);
        Persisted.Overwrite(actingAdmin, new { PasswordHash = hasher.Hash(ActingPassword) });
        currentUser.Id = actingAdmin.Id;
        events = new CommittedEvents(
            new AccountSecurityNotifications(
                users,
                new AccountSecurityNotifier(
                    emailSender,
                    clock,
                    composer,
                    NullLogger<AccountSecurityNotifier>.Instance
                ),
                NullLogger<AccountSecurityNotifications>.Instance
            )
        );
        sut = new SetAdminCommandHandler(
            users,
            currentUser,
            PasswordGuards.Create(hasher, uow, clock, users: users, currentUser: currentUser),
            clock
        );
    }

    private Task<Result> HandleAsync(Guid userId, bool isAdmin, string? currentPassword = null)
    {
        return sut.HandleAsync(
            new SetAdminRequest(isAdmin, currentPassword).ToCommand(UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private ComposedAccountEmail AdminAlert(User user, AccountSecurityChange change)
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
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(actingAdmin, null);

        var result = await HandleAsync(Guid.NewGuid(), true, ActingPassword);

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData(true, ActingPassword)]
    [InlineData(true, "wrong-password")]
    [InlineData(false, null)]
    public async Task HandleAsyncOwnAccountReturnsForbiddenWithoutCheckingThePassword(
        bool isAdmin,
        string? currentPassword
    )
    {
        users.FindReturns(actingAdmin);

        var result = await HandleAsync(actingAdmin.Id.Value, isAdmin, currentPassword);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.UserSelfAdminChangeForbidden);
        actingAdmin.IsAdmin.Should().BeTrue();
        actingAdmin.PasswordLockout.FailedAttempts.Should().Be(0);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
        await events.PublishAsync(actingAdmin);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncNonAdministratorGrantingThemselvesReturnsForbidden()
    {
        var member = NewUser(isAdmin: false);
        Persisted.Overwrite(member, new { PasswordHash = hasher.Hash(ActingPassword) });
        currentUser.Id = member.Id;
        currentUser.IsAdmin = false;
        users.FindReturns(member);

        var result = await HandleAsync(member.Id.Value, true, ActingPassword);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.UserSelfAdminChangeForbidden);
        member.IsAdmin.Should().BeFalse();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGrantWithCorrectPasswordGrantsAndSaves()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(actingAdmin, user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await HandleAsync(user.Id.Value, true, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeTrue();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await events.PublishAsync(user);
        composer.Composed.Should().Equal(AdminAlert(user, AccountSecurityChange.AdminGranted));
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be(user.Email!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleAsyncGrantWithoutPasswordReturnsBadRequest(string? currentPassword)
    {
        var result = await HandleAsync(Guid.NewGuid(), true, currentPassword);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGrantWithIncorrectPasswordReturnsBadRequest()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id.Value, true, "wrong-password");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.IsAdmin.Should().BeFalse();
        actingAdmin.PasswordLockout.FailedAttempts.Should().Be(1);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGrantActingUserWithoutPasswordReturnsBadRequest()
    {
        Persisted.Overwrite(actingAdmin, new { PasswordHash = default(string?) });
        var user = NewUser(isAdmin: false);
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id.Value, true, ActingPassword);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.IsAdmin.Should().BeFalse();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGrantActingUserMissingReturnsBadRequest()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(null, user);

        var result = await HandleAsync(user.Id.Value, true, ActingPassword);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.IsAdmin.Should().BeFalse();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncFlagUnchangedIsNoopAndDoesNotSave()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id.Value, true, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        await AssertNotSavedAsync();
        await events.PublishAsync(user);
        emailSender
            .Sent.Should()
            .BeEmpty("an unchanged flag is not a security event worth reporting");
    }

    [Fact]
    public async Task HandleAsyncRevokeRevokesWithoutLoadingTheActingUser()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(user);

        var result = await HandleAsync(user.Id.Value, false);

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeFalse();
        await users.Received(1).GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
        await users.Received(1).GetByIdAsync(user.Id, Arg.Any<CancellationToken>());
        await events.PublishAsync(user);
        composer.Composed.Should().Equal(AdminAlert(user, AccountSecurityChange.AdminRevoked));
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be(user.Email!.Value);
    }

    [Fact]
    public async Task HandleAsyncRevokeInitialAdministratorReturnsForbidden()
    {
        var user = NewUser(id: KnownIds.Users.InitialAdministrator, isAdmin: true);
        users.FindReturns(user);

        var result = await HandleAsync(user.Id.Value, false);

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.UserCannotRemoveInitialAdmin);
        user.IsAdmin.Should().BeTrue();
        await AssertNotSavedAsync();
        await events.PublishAsync(user);
        emailSender.Sent.Should().BeEmpty();
    }
}
