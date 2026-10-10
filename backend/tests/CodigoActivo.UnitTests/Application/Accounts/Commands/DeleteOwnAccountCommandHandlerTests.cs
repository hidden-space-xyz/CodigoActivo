using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;
using TwoFactorMethod = CodigoActivo.Domain.Users.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class DeleteOwnAccountCommandHandlerTests
{
    private const string Password = "Str0ngPass!23";
    private const string EmailCode = "482913";
    private const string Secret = "JBSWY3DPEHPK3PXP";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IDeletedAccountRepository deletedAccounts =
        Substitute.For<IDeletedAccountRepository>();
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestCurrentUser currentUser = new();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TestClock clock = new();
    private readonly TwoFactorOptions options = new() { MaxFailedAttempts = 2 };
    private readonly DeleteOwnAccountCommandHandler sut;

    public DeleteOwnAccountCommandHandlerTests()
    {
        var hasher = new FakePasswordHasher();
        sut = new DeleteOwnAccountCommandHandler(
            users,
            currentUser,
            AccountErasers.Create(users, deletedAccounts, erasureStore, uow),
            uow,
            clock,
            PasswordGuards.Create(hasher, uow, clock),
            new OtpValidator(new FakeOneTimeCodeHasher()),
            new AuthenticatorCodeVerifier(
                totp,
                new FakeSecretProtector(),
                clock,
                NullLogger<AuthenticatorCodeVerifier>.Instance
            ),
            options,
            NullLogger<DeleteOwnAccountCommandHandler>.Instance
        );
    }

    private User Signed(
        bool isAdmin = false,
        string? passwordHash = FakePasswordHasher.Prefix + Password
    )
    {
        var user = NewUser(isAdmin: isAdmin, passwordHash: passwordHash);
        Persisted.Overwrite(
            user,
            new
            {
                LoginCodeHash = FakeOneTimeCodeHasher.Prefix + EmailCode,
                LoginCodeExpiresAt = clock.UtcNow.AddMinutes(5),
            }
        );
        users.Finds(user);
        return user;
    }

    private User SignedWithAuthenticator(long? lastUsedStep = null)
    {
        var user = NewUser(passwordHash: FakePasswordHasher.Prefix + Password);
        Persisted.Overwrite(
            user,
            new
            {
                TwoFactorMethod = TwoFactorMethod.Authenticator,
                AuthenticatorKey = FakeSecretProtector.Prefix + Secret,
                AuthenticatorLastUsedStep = lastUsedStep,
            }
        );
        users.Finds(user);
        return user;
    }

    private Task<Result> DeleteAsync(
        Guid userId,
        string password = Password,
        string code = EmailCode
    )
    {
        currentUser.Id = UserId.From(userId);
        return sut.HandleAsync(
            new DeleteAccountRequest(password, code).ToCommand(),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private void AssertNothingRemoved()
    {
        users.DidNotReceiveWithAnyArgs().Remove(Arg.Any<User>());
        _ = erasureStore
            .DidNotReceiveWithAnyArgs()
            .CaptureLegalCopyAsync(default, default!, default);
        _ = deletedAccounts.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    private async Task AssertErasedAsync(User user)
    {
        await erasureStore
            .Received(1)
            .CaptureLegalCopyAsync(
                user.Id,
                Arg.Is<AccountErasure>(erasure =>
                    erasure.Origin == AccountDeletionOrigin.Self
                    && erasure.ActorId == user.Id
                    && erasure.DeletedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
        await deletedAccounts
            .Received(1)
            .AddAsync(
                Arg.Is<DeletedAccount>(copy =>
                    copy.Id == user.Id && copy.Data == AccountErasers.LegalCopy
                ),
                Arg.Any<CancellationToken>()
            );
        users.Received(1).Remove(user);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.Finds(null);

        var result = await DeleteAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        AssertNothingRemoved();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncInitialAdministratorReturnsForbiddenWithoutCheckingThePassword()
    {
        var result = await DeleteAsync(KnownIds.Users.InitialAdministrator);

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.UserDeleteInitialAdminForbidden);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        AssertNothingRemoved();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdministratorRemovesTheAccount()
    {
        var user = Signed(isAdmin: true);

        var result = await DeleteAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user);
    }

    [Fact]
    public async Task HandleAsyncWrongPasswordReturnsBadRequestWithoutCountingACodeFailure()
    {
        var user = Signed();

        var result = await DeleteAsync(user.Id.Value, password: "WrongPassword!");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.PasswordFailedAttempts.Should().Be(1);
        AssertNothingRemoved();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutPasswordReturnsBadRequest()
    {
        var user = Signed(passwordHash: null);

        var result = await DeleteAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbiddenEvenWithTheRightCode()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(1) });

        var result = await DeleteAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.TwoFactorLocked);
        AssertNothingRemoved();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockoutStoredByAParallelAttemptRefusesTheRightCode()
    {
        var user = Signed();
        users
            .LockAsync(user, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Persisted.Overwrite(
                    user,
                    new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(15) }
                );
                return true;
            });

        var result = await DeleteAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.TwoFactorLocked);
        AssertNothingRemoved();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWrongEmailCodeCountsTheFailureAndKeepsTheAccount()
    {
        var user = Signed();

        var result = await DeleteAsync(user.Id.Value, code: "000000");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorFailedAttempts.Should().Be(1);
        AssertNothingRemoved();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncExpiredEmailCodeIsRejected()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { LoginCodeExpiresAt = clock.UtcNow.AddMinutes(-1) });

        var result = await DeleteAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncRepeatedWrongCodesLockTheSecondFactor()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { TwoFactorFailedAttempts = 1 });

        await DeleteAsync(user.Id.Value, code: "000000");

        user.TwoFactorLockedUntil.Should().Be(clock.UtcNow + options.LockoutDuration);
        user.LoginCodeHash.Should().BeNull("locking discards the challenged code");
    }

    [Fact]
    public async Task HandleAsyncWrongAuthenticatorCodeReturnsBadRequest()
    {
        var user = SignedWithAuthenticator();
        totp.MatchStep(Secret, "000000", clock.UtcNow).Returns(default(long?));

        var result = await DeleteAsync(user.Id.Value, code: "000000");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorFailedAttempts.Should().Be(1);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncReplayedAuthenticatorCodeIsRejected()
    {
        var user = SignedWithAuthenticator(lastUsedStep: 50);
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(50);

        var result = await DeleteAsync(user.Id.Value, code: "123456");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorCodeCorrectRemovesTheAccount()
    {
        var user = SignedWithAuthenticator(lastUsedStep: 50);
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(51);

        var result = await DeleteAsync(user.Id.Value, code: "123456");

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user);
    }

    [Fact]
    public async Task HandleAsyncAccountErasedConcurrentlyReturnsNotFoundAndKeepsTheCache()
    {
        var user = Signed();
        erasureStore.LockHouseholdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await DeleteAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncPasswordAndEmailCodeCorrectErasesTheAccount()
    {
        var user = Signed();

        var result = await DeleteAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user);
    }
}
