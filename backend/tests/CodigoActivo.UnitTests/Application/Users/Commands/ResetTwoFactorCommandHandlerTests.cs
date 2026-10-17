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

public sealed class ResetTwoFactorCommandHandlerTests
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
    private readonly ResetTwoFactorCommandHandler sut;

    public ResetTwoFactorCommandHandlerTests()
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
                )
            )
        );
        sut = new ResetTwoFactorCommandHandler(
            users,
            PasswordGuards.Create(hasher, uow, clock, users: users, currentUser: currentUser),
            clock
        );
    }

    private Task<Result> HandleAsync(Guid userId, string currentPassword)
    {
        return sut.HandleAsync(
            new ResetTwoFactorRequest(currentPassword).ToCommand(UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    private static User UserWithAuthenticator()
    {
        var user = NewUser();
        Persisted.Overwrite(
            user,
            new
            {
                TwoFactorMethod = TwoFactorMethod.Authenticator,
                Authenticator = new
                {
                    Key = "protected:secret",
                    LastUsedStep = (long?)12,
                    PendingKey = "protected:pending",
                },
                LoginCode = new { Hash = "code" },
                TwoFactorLockout = new
                {
                    FailedAttempts = 3,
                    LockedUntil = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
                },
            }
        );
        return user;
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncWrongActingPasswordReturnsBadRequestWithoutLoadingTheTarget()
    {
        var user = UserWithAuthenticator();
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id.Value, "wrong");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        actingAdmin.PasswordLockout.FailedAttempts.Should().Be(1);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncActingUserMissingReturnsBadRequest()
    {
        users.FindReturns(null, UserWithAuthenticator());

        var result = await HandleAsync(Guid.NewGuid(), ActingPassword);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(actingAdmin, null);

        var result = await HandleAsync(Guid.NewGuid(), ActingPassword);

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncCorrectPasswordReturnsUserToEmailAndClearsEverything()
    {
        var user = UserWithAuthenticator();
        users.FindReturns(actingAdmin, user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await HandleAsync(user.Id.Value, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.Authenticator.Key.Should().BeNull();
        user.Authenticator.LastUsedStep.Should().BeNull();
        user.Authenticator.PendingKey.Should().BeNull();
        user.LoginCode.Hash.Should().BeNull();
        user.TwoFactorLockout.FailedAttempts.Should().Be(0);
        user.TwoFactorLockout.LockedUntil.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await events.PublishAsync(user);
        composer
            .Composed.Should()
            .Equal(
                new ComposedAccountEmail(
                    nameof(IAccountEmailComposer.SecurityAlert),
                    new EmailRecipient(user.Email!.Value, user.FirstName)
                )
                {
                    Change = AccountSecurityChange.TwoFactorReset,
                    OccurredAt = clock.UtcNow,
                }
            );
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be(user.Email!.Value);
    }

    [Fact]
    public async Task HandleAsyncEmailUserOnlyClearsTheLockWithoutNotifying()
    {
        var user = NewUser();
        Persisted.Overwrite(
            user,
            new
            {
                TwoFactorLockout = new
                {
                    FailedAttempts = 5,
                    LockedUntil = clock.UtcNow.AddMinutes(10),
                },
            }
        );
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id.Value, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        user.TwoFactorLockout.FailedAttempts.Should().Be(0);
        user.TwoFactorLockout.LockedUntil.Should().BeNull();
        await events.PublishAsync(user);
        emailSender.Sent.Should().BeEmpty();
    }
}
