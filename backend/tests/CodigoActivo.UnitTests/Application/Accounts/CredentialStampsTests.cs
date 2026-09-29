using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using CodigoActivo.Application.Accounts;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Accounts;

public sealed class CredentialStampsTests
{
    [Fact]
    public void ForPasswordHashIsItsUppercaseHexSha256()
    {
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("fake:secret")));

        CredentialStamps.For("fake:secret").Should().Be(expected).And.HaveLength(64);
    }

    [Fact]
    public void ForDifferentPasswordHashesGivesDifferentStamps()
    {
        CredentialStamps.For("fake:secret").Should().NotBe(CredentialStamps.For("fake:other"));
        CredentialStamps.For("fake:secret").Should().Be(CredentialStamps.For("fake:secret"));
    }

    [Fact]
    public void ForMissingPasswordHashThrows()
    {
        var act = () => CredentialStamps.For(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MatchesSameStampIsAccepted()
    {
        var stamp = CredentialStamps.For("fake:secret");

        CredentialStamps.Matches(stamp, CredentialStamps.For("fake:secret")).Should().BeTrue();
    }

    [Fact]
    public void MatchesStampOfAnotherPasswordOrLengthIsRefused()
    {
        var stamp = CredentialStamps.For("fake:secret");

        CredentialStamps.Matches(CredentialStamps.For("fake:other"), stamp).Should().BeFalse();
        CredentialStamps.Matches(stamp[..^1], stamp).Should().BeFalse();
        CredentialStamps.Matches(string.Empty, stamp).Should().BeFalse();
    }

    [Fact]
    public void MatchesMissingStampThrows()
    {
        var withoutPresented = () => CredentialStamps.Matches(null!, "A");
        var withoutExpected = () => CredentialStamps.Matches("A", null!);

        withoutPresented.Should().Throw<ArgumentNullException>();
        withoutExpected.Should().Throw<ArgumentNullException>();
    }
}
