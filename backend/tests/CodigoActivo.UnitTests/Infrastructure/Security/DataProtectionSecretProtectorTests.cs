using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class DataProtectionSecretProtectorTests
{
    private static DataProtectionSecretProtector NewProtector(
        ILogger<DataProtectionSecretProtector>? logger = null
    )
    {
        return new DataProtectionSecretProtector(
            new EphemeralDataProtectionProvider(),
            logger ?? NullLogger<DataProtectionSecretProtector>.Instance
        );
    }

    [Fact]
    public void ProtectHidesTheSecretAndTryUnprotectRestoresIt()
    {
        var sut = NewProtector();

        var protectedValue = sut.Protect("JBSWY3DPEHPK3PXP");

        protectedValue.Should().NotContain("JBSWY3DPEHPK3PXP");
        sut.TryUnprotect(protectedValue, out var plaintext).Should().BeTrue();
        plaintext.Should().Be("JBSWY3DPEHPK3PXP");
    }

    [Fact]
    public void TryUnprotectValueFromAnotherKeyRingReportsItUnreadableAndLogsIt()
    {
        var logger = new RecordingLogger<DataProtectionSecretProtector>();
        var protectedValue = NewProtector().Protect("JBSWY3DPEHPK3PXP");

        var read = NewProtector(logger).TryUnprotect(protectedValue, out var plaintext);

        read.Should().BeFalse();
        plaintext.Should().BeNull();
        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry
            .Message.Should()
            .Contain("could not be decrypted")
            .And.Contain("CryptographicException");
    }

    [Fact]
    public void ConstructorNullProviderThrows()
    {
        var act = () =>
            new DataProtectionSecretProtector(
                null!,
                NullLogger<DataProtectionSecretProtector>.Instance
            );

        act.Should().Throw<ArgumentNullException>();
    }
}
