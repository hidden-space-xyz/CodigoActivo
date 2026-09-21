using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Domain.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Auth;

public sealed class AuthenticatorCodeVerifierTests
{
    private const string Secret = "JBSWY3DPEHPK3PXP";
    private const string ProtectedSecret = FakeSecretProtector.Prefix + Secret;

    private readonly ITotpService totp = Substitute.For<ITotpService>();
    private readonly TestClock clock = new();
    private readonly AuthenticatorCodeVerifier sut;

    public AuthenticatorCodeVerifierTests()
    {
        sut = new AuthenticatorCodeVerifier(
            totp,
            new FakeSecretProtector(),
            clock,
            NullLogger<AuthenticatorCodeVerifier>.Instance
        );
    }

    [Fact]
    public void MatchNoKeyReturnsNullWithoutConsultingTotp()
    {
        sut.Match(null, "123456", null).Should().BeNull();

        totp.DidNotReceiveWithAnyArgs().MatchStep(default!, default!, default);
    }

    [Fact]
    public void MatchUnreadableKeyReturnsNull()
    {
        sut.Match("not-protected", "123456", null).Should().BeNull();

        totp.DidNotReceiveWithAnyArgs().MatchStep(default!, default!, default);
    }

    [Fact]
    public void MatchDecryptsTheKeyAndReturnsTheMatchedStep()
    {
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(7);

        sut.Match(ProtectedSecret, "123456", null).Should().Be(7);
    }

    [Theory]
    [InlineData(7, 7)]
    [InlineData(7, 8)]
    public void MatchStepAlreadyUsedOrOlderReturnsNull(long matched, long lastUsed)
    {
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(matched);

        sut.Match(ProtectedSecret, "123456", lastUsed).Should().BeNull();
    }

    [Fact]
    public void MatchNewerStepThanLastUsedIsAccepted()
    {
        totp.MatchStep(Secret, "123456", clock.UtcNow).Returns(9);

        sut.Match(ProtectedSecret, "123456", 8).Should().Be(9);
    }

    [Fact]
    public void MatchCodeRejectedByTotpReturnsNull()
    {
        totp.MatchStep(Secret, "000000", clock.UtcNow).Returns(default(long?));

        sut.Match(ProtectedSecret, "000000", null).Should().BeNull();
    }
}
