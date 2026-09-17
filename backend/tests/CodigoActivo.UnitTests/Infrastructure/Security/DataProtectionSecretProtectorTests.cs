using System.Security.Cryptography;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class DataProtectionSecretProtectorTests
{
    private static DataProtectionSecretProtector NewProtector()
    {
        return new DataProtectionSecretProtector(new EphemeralDataProtectionProvider());
    }

    [Fact]
    public void ProtectHidesTheSecretAndUnprotectRestoresIt()
    {
        var sut = NewProtector();

        var protectedValue = sut.Protect("JBSWY3DPEHPK3PXP");

        protectedValue.Should().NotContain("JBSWY3DPEHPK3PXP");
        sut.Unprotect(protectedValue).Should().Be("JBSWY3DPEHPK3PXP");
    }

    [Fact]
    public void UnprotectValueFromAnotherKeyRingThrows()
    {
        var protectedValue = NewProtector().Protect("JBSWY3DPEHPK3PXP");

        var act = () => NewProtector().Unprotect(protectedValue);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void ConstructorNullProviderThrows()
    {
        var act = () => new DataProtectionSecretProtector(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
