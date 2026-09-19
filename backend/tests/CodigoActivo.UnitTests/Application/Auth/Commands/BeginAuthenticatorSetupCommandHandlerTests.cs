using AwesomeAssertions;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class BeginAuthenticatorSetupCommandHandlerTests
{
    private const string Secret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TwoFactorOptions options = new() { Issuer = "Código Activo" };
    private readonly BeginAuthenticatorSetupCommandHandler sut;

    public BeginAuthenticatorSetupCommandHandlerTests()
    {
        totp.GenerateSecret().Returns(Secret);
        sut = new BeginAuthenticatorSetupCommandHandler(
            users,
            uow,
            clock,
            PasswordGuards.Create(new FakePasswordHasher(), uow, clock),
            totp,
            new FakeSecretProtector(),
            options,
            NullLogger<BeginAuthenticatorSetupCommandHandler>.Instance
        );
    }

    private Task<Result<AuthenticatorSetupResponse>> SetupAsync(Guid userId, string password)
    {
        return sut.HandleAsync(
            new BeginAuthenticatorSetupCommand(userId, new AuthenticatorSetupRequest(password)),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await SetupAsync(Guid.NewGuid(), "password123");

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
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
        var user = users.FindReturns(NewUser());

        var result = await SetupAsync(user.Id, password);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.PendingAuthenticatorKey.Should().BeNull();
        user.PasswordFailedAttempts.Should().Be(countedFailures);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordNotSetReturnsBadRequest()
    {
        var user = users.FindReturns(NewUser(passwordHash: null));

        var result = await SetupAsync(user.Id, "password123");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
    }

    [Fact]
    public async Task HandleAsyncCorrectPasswordStoresProtectedPendingKeyAndReturnsEnrollmentData()
    {
        var user = users.FindReturns(NewUser(email: "ana@test.com"));

        var result = await SetupAsync(user.Id, "password123");

        result.IsSuccess.Should().BeTrue();
        result.Value.SharedKey.Should().Be("JBSW Y3DP EHPK 3PXP JBSW Y3DP EHPK 3PXP");
        result
            .Value.AuthenticatorUri.Should()
            .Be(
                $"otpauth://totp/C%C3%B3digo%20Activo:ana%40test.com?secret={Secret}&issuer=C%C3%B3digo%20Activo&algorithm=SHA1&digits=6&period=30"
            );
        user.PendingAuthenticatorKey.Should().Be(FakeSecretProtector.Prefix + Secret);
        user.PendingAuthenticatorExpiresAt.Should().Be(clock.UtcNow + options.SetupLifetime);
        user.AuthenticatorKey.Should().BeNull("the key only activates after confirmation");
        user.TwoFactorMethod.Should().Be(CodigoActivo.Domain.Entities.TwoFactorMethod.Email);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncUserWithoutEmailLabelsTheAccountWithItsIdentifier()
    {
        var user = users.FindReturns(NewUser(email: null));

        var result = await SetupAsync(user.Id, "password123");

        result.Value.AuthenticatorUri.Should().Contain($":{user.Id}?secret=");
    }
}
