using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class DeleteOwnAccountCommandHandlerTests
{
    private const string Password = "Str0ngPass!23";
    private const string EmailCode = "482913";
    private const string Secret = "JBSWY3DPEHPK3PXP";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IDeletedAccountRepository deletedAccounts =
        Substitute.For<IDeletedAccountRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TestClock clock = new();
    private readonly TwoFactorOptions options = new() { MaxFailedAttempts = 2 };
    private readonly DeleteOwnAccountCommandHandler sut;

    public DeleteOwnAccountCommandHandlerTests()
    {
        var hasher = new FakePasswordHasher();
        deletedAccounts
            .EraseAsync(Arg.Any<User>(), Arg.Any<AccountErasure>(), Arg.Any<CancellationToken>())
            .Returns(true);
        sut = new DeleteOwnAccountCommandHandler(
            users,
            deletedAccounts,
            uow,
            clock,
            PasswordGuards.Create(hasher, uow, clock),
            new OtpValidator(clock, hasher),
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
        user.LoginCodeHash = FakePasswordHasher.Prefix + EmailCode;
        user.LoginCodeExpiresAt = clock.UtcNow.AddMinutes(5);
        users.Finds(user);
        return user;
    }

    private User SignedWithAuthenticator(long? lastUsedStep = null)
    {
        var user = NewUser(passwordHash: FakePasswordHasher.Prefix + Password);
        user.TwoFactorMethod = TwoFactorMethod.Authenticator;
        user.AuthenticatorKey = FakeSecretProtector.Prefix + Secret;
        user.AuthenticatorLastUsedStep = lastUsedStep;
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
        _ = deletedAccounts.DidNotReceiveWithAnyArgs().EraseAsync(default!, default!, default);
    }

    private Task<bool> AssertErasedAsync(User user)
    {
        users.DidNotReceiveWithAnyArgs().Remove(Arg.Any<User>());
        return deletedAccounts
            .Received(1)
            .EraseAsync(
                user,
                new AccountErasure(AccountDeletionOrigin.Self, user.Id, clock.UtcNow),
                Arg.Any<CancellationToken>()
            );
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
            .FindAsync(default!, TestContext.Current.CancellationToken);
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

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
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

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbiddenEvenWithTheRightCode()
    {
        var user = Signed();
        user.TwoFactorLockedUntil = clock.UtcNow.AddMinutes(1);

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

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorFailedAttempts.Should().Be(1);
        AssertNothingRemoved();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertCacheKeptAsync();
    }

    [Fact]
    public async Task HandleAsyncExpiredEmailCodeIsRejected()
    {
        var user = Signed();
        user.LoginCodeExpiresAt = clock.UtcNow.AddMinutes(-1);

        var result = await DeleteAsync(user.Id);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncRepeatedWrongCodesLockTheSecondFactor()
    {
        var user = Signed();
        user.TwoFactorFailedAttempts = 1;

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

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
        user.TwoFactorFailedAttempts.Should().Be(1);
        AssertNothingRemoved();
    }

    [Fact]
    public async Task HandleAsyncReplayedAuthenticatorCodeIsRejected()
    {
        var user = SignedWithAuthenticator(lastUsedStep: 50);
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(50);

        var result = await DeleteAsync(user.Id, code: "123456");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.TwoFactorCodeInvalid);
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
        deletedAccounts
            .EraseAsync(user, Arg.Any<AccountErasure>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await DeleteAsync(user.Id);

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
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
