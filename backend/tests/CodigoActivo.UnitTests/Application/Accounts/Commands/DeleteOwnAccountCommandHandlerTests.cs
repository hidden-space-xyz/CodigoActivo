using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

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
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TestClock clock = new();
    private readonly TwoFactorOptions options = new() { MaxFailedAttempts = 2 };
    private readonly DeleteOwnAccountCommandHandler sut;

    public DeleteOwnAccountCommandHandlerTests()
    {
        var hasher = new FakePasswordHasher();
        sut = new DeleteOwnAccountCommandHandler(
            users,
            AccountErasers.Create(users, deletedAccounts, erasureStore, uow),
            uow,
            clock,
            PasswordGuards.Create(hasher, uow, clock),
            new OtpValidator(hasher),
            new AuthenticatorCodeVerifier(
                totp,
                new FakeSecretProtector(),
                clock,
                NullLogger<AuthenticatorCodeVerifier>.Instance
            ),
            options,
            cacheInvalidator,
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
                LoginCodeHash = FakePasswordHasher.Prefix + EmailCode,
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
        return sut.HandleAsync(
            new DeleteOwnAccountCommand(userId, new DeleteAccountRequest(password, code)),
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

    private ValueTask AssertCacheKeptAsync()
    {
        return cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.Finds(null);

        var result = await DeleteAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        AssertNothingRemoved();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncInitialAdministratorReturnsForbiddenWithoutCheckingThePassword()
    {
        var result = await DeleteAsync(SeedIds.Users.InitialAdministrator);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserDeleteInitialAdminForbidden);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        AssertNothingRemoved();
        await AssertNotSavedAsync();
        await AssertCacheKeptAsync();
    }

    [Fact]
    public async Task HandleAsyncAdministratorRemovesTheAccount()
    {
        var user = Signed(isAdmin: true);

        var result = await DeleteAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user);
    }

    [Fact]
    public async Task HandleAsyncWrongPasswordReturnsBadRequestWithoutCountingACodeFailure()
    {
        var user = Signed();

        var result = await DeleteAsync(user.Id, password: "WrongPassword!");

        result.ShouldFail(ErrorKind.Validation, ErrorCode.UserCurrentPasswordIncorrect);
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

        var result = await DeleteAsync(user.Id);

        result.ShouldFail(ErrorKind.Validation, ErrorCode.UserCurrentPasswordIncorrect);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbiddenEvenWithTheRightCode()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { TwoFactorLockedUntil = clock.UtcNow.AddMinutes(1) });

        var result = await DeleteAsync(user.Id);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.TwoFactorLocked);
        AssertNothingRemoved();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWrongEmailCodeCountsTheFailureAndKeepsTheAccount()
    {
        var user = Signed();

        var result = await DeleteAsync(user.Id, code: "000000");

        result.ShouldFail(ErrorKind.Validation, ErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorFailedAttempts.Should().Be(1);
        AssertNothingRemoved();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertCacheKeptAsync();
    }

    [Fact]
    public async Task HandleAsyncExpiredEmailCodeIsRejected()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { LoginCodeExpiresAt = clock.UtcNow.AddMinutes(-1) });

        var result = await DeleteAsync(user.Id);

        result.ShouldFail(ErrorKind.Validation, ErrorCode.TwoFactorCodeInvalid);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncRepeatedWrongCodesLockTheSecondFactor()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { TwoFactorFailedAttempts = 1 });

        await DeleteAsync(user.Id, code: "000000");

        user.TwoFactorLockedUntil.Should().Be(clock.UtcNow + options.LockoutDuration);
        user.LoginCodeHash.Should().BeNull("locking discards the challenged code");
    }

    [Fact]
    public async Task HandleAsyncWrongAuthenticatorCodeReturnsBadRequest()
    {
        var user = SignedWithAuthenticator();
        totp.MatchStep(Secret, "000000", clock.UtcNow).Returns(default(long?));

        var result = await DeleteAsync(user.Id, code: "000000");

        result.ShouldFail(ErrorKind.Validation, ErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorFailedAttempts.Should().Be(1);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncReplayedAuthenticatorCodeIsRejected()
    {
        var user = SignedWithAuthenticator(lastUsedStep: 50);
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(50);

        var result = await DeleteAsync(user.Id, code: "123456");

        result.ShouldFail(ErrorKind.Validation, ErrorCode.TwoFactorCodeInvalid);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorCodeCorrectRemovesTheAccount()
    {
        var user = SignedWithAuthenticator(lastUsedStep: 50);
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(51);

        var result = await DeleteAsync(user.Id, code: "123456");

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user);
    }

    [Fact]
    public async Task HandleAsyncAccountErasedConcurrentlyReturnsNotFoundAndKeepsTheCache()
    {
        var user = Signed();
        erasureStore.LockHouseholdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await DeleteAsync(user.Id);

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        AssertNothingRemoved();
        await AssertCacheKeptAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordAndEmailCodeCorrectErasesAndInvalidatesCache()
    {
        var user = Signed();

        var result = await DeleteAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user);
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.SequenceEqual(CacheTags.Erasure)
                )
            );
    }
}
