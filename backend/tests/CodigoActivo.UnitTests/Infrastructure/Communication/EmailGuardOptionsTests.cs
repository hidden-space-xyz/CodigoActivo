using AwesomeAssertions;
using CodigoActivo.Infrastructure.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailGuardOptionsTests
{
    [Fact]
    public void EffectiveCredentialReserveNotBelowTheGlobalBurstClampsSoAccountEmailCanStillSend()
    {
        var options = new EmailGuardOptions { GlobalBurst = 50, GlobalCredentialReserve = 80 };

        options.EffectiveCredentialReserve.Should().Be(49);
        options.EffectiveTwoFactorReserve.Should().Be(24);
    }

    [Fact]
    public void EffectiveCredentialReserveShippedKeepsHalfOfItForLoginCodes()
    {
        var options = new EmailGuardOptions();

        options.EffectiveCredentialReserve.Should().Be(200);
        options.EffectiveTwoFactorReserve.Should().Be(100);
    }
}
