using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class BeginAuthenticatorSetupCommandHandlerTests
{
    private const string Secret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
    private const string EmailCode = "482913";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TwoFactorOptions options = new() { MaxFailedAttempts = 2 };
    private readonly TestCurrentUser currentUser = new();
    private readonly BeginAuthenticatorSetupCommandHandler sut;

    public BeginAuthenticatorSetupCommandHandlerTests()
    {
        totp.GenerateSecret().Returns(Secret);
        uow.RunsTransactionsOf<Result<AuthenticatorSetup>>();
        sut = new BeginAuthenticatorSetupCommandHandler(
            users,
            currentUser,
            AccountLocks.Create(users, uow),
            clock,
            PasswordGuards.Create(new FakePasswordHasher(), uow, clock),
            SecondFactorGuards.Create(uow, clock, totp, options),
            totp,
            new FakeSecretProtector(),
            options
        );
    }

    private User WithEmailedCode(User user)
    {
        Persisted.Overwrite(
            user,
            new
            {
                LoginCode = new
                {
                    Hash = FakeOneTimeCodeHasher.Prefix + EmailCode,
                    ExpiresAt = clock.UtcNow.AddMinutes(5),
                },
            }
        );
        return users.FindReturns(user);
    }

    private Task<Result<AuthenticatorSetup>> SetupAsync(
        Guid userId,
        string password,
        string code = EmailCode
    )
    {
        currentUser.Id = UserId.From(userId);
        return sut.HandleAsync(
            new AuthenticatorSetupRequest(password, code).ToCommand(),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotSaveAsync();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await SetupAsync(Guid.NewGuid(), "password123");

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData("wrong", 1)]
    [InlineData("", 0)]
    public async Task HandleAsyncWrongPasswordReturnsBadRequestWithoutStoringAKey(
        string password,
        int countedFailures
    )
    {
        var user = WithEmailedCode(NewUser());

        var result = await SetupAsync(user.Id.Value, password);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.Authenticator.PendingKey.Should().BeNull();
        user.PasswordLockout.FailedAttempts.Should().Be(countedFailures);
        user.LoginCode.Hash.Should().NotBeNull("a wrong password does not spend the code");
        user.TwoFactorLockout.FailedAttempts.Should().Be(0);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordNotSetReturnsBadRequest()
    {
        var user = WithEmailedCode(NewUser(passwordHash: null));

        var result = await SetupAsync(user.Id.Value, "password123");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
    }

    [Fact]
    public async Task HandleAsyncCorrectPasswordAndCodeStoresProtectedPendingKeyAndSpendsTheCode()
    {
        var user = WithEmailedCode(NewUser(email: "ana@test.com"));

        var result = await SetupAsync(user.Id.Value, "password123");

        result.IsSuccess.Should().BeTrue();
        result.Value.SharedKey.Should().Be("JBSW Y3DP EHPK 3PXP JBSW Y3DP EHPK 3PXP");
        result
            .Value.AuthenticatorUri.Should()
            .Be(
                $"otpauth://totp/C%C3%B3digo%20Activo:ana%40test.com?secret={Secret}&issuer=C%C3%B3digo%20Activo&algorithm=SHA1&digits=6&period=30"
            );
        user.Authenticator.PendingKey.Should().Be(FakeSecretProtector.Prefix + Secret);
        user.Authenticator.PendingExpiresAt.Should().Be(clock.UtcNow + options.SetupLifetime);
        user.Authenticator.Key.Should().BeNull("the key only activates after confirmation");
        user.TwoFactorMethod.Should().Be(CodigoActivo.Domain.Users.TwoFactorMethod.Email);
        user.LoginCode.Hash.Should().BeNull("the emailed code is spent by the enrollment");
        await users.Received(1).LockAsync(user, Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncWrongCodeCountsTheFailureWithoutStoringAKey()
    {
        var user = WithEmailedCode(NewUser());

        var result = await SetupAsync(user.Id.Value, "password123", code: "000000");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.Authenticator.PendingKey.Should().BeNull();
        user.TwoFactorLockout.FailedAttempts.Should().Be(1);
        user.LoginCode.Hash.Should().NotBeNull("a wrong guess must not consume the code");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncRepeatedWrongCodesLockTheSecondFactorAndRefuseTheRightCode()
    {
        var user = WithEmailedCode(NewUser());

        for (var attempt = 0; attempt < options.MaxFailedAttempts; attempt++)
        {
            var wrong = await SetupAsync(user.Id.Value, "password123", code: "000000");
            wrong.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        }

        var result = await SetupAsync(user.Id.Value, "password123");

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.TwoFactorLocked);
        user.Authenticator.PendingKey.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncExpiredCodeReturnsBadRequestWithoutStoringAKey()
    {
        var user = WithEmailedCode(NewUser());
        clock.UtcNow += TimeSpan.FromMinutes(6);

        var result = await SetupAsync(user.Id.Value, "password123");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.TwoFactorCodeInvalid);
        user.Authenticator.PendingKey.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorAlreadyActiveReturnsConflictBeforeThePassword()
    {
        var user = users.FindReturns(NewUserWithAuthenticator(Secret));

        var result = await SetupAsync(user.Id.Value, "wrong");

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.AuthenticatorAlreadyEnabled);
        user.Authenticator.PendingKey.Should().BeNull();
        user.Authenticator.Key.Should().Be(FakeSecretProtector.Prefix + Secret);
        user.PasswordLockout.FailedAttempts.Should().Be(0);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorEnabledBeforeTheLockReturnsConflictWithoutCheckingTheCode()
    {
        var user = WithEmailedCode(NewUser());
        users.ReloadsAs(
            user,
            new
            {
                TwoFactorMethod = CodigoActivo.Domain.Users.TwoFactorMethod.Authenticator,
                Authenticator = new { Key = FakeSecretProtector.Prefix + Secret },
            }
        );

        var result = await SetupAsync(user.Id.Value, "password123");

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.AuthenticatorAlreadyEnabled);
        user.Authenticator.PendingKey.Should().BeNull();
        user.TwoFactorLockout.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncUserWithoutEmailLabelsTheAccountWithItsIdentifier()
    {
        var user = WithEmailedCode(NewUser(email: null));

        var result = await SetupAsync(user.Id.Value, "password123");

        result.Value.AuthenticatorUri.Should().Contain($":{user.Id}?secret=");
    }
}
