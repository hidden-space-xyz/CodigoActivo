using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher sut = new();

    [Fact]
    public void VerifySamePasswordAsHashReturnsTrue()
    {
        var hash = sut.Hash("correct horse battery staple");

        sut.Verify("correct horse battery staple", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyWrongPasswordReturnsFalse()
    {
        var hash = sut.Hash("s3cret");

        sut.Verify("not-the-password", hash).Should().BeFalse();
    }

    [Fact]
    public void HashAnyPasswordProducesSixPartArgon2idShape()
    {
        var hash = sut.Hash("whatever");

        var parts = hash.Split('$');
        parts.Should().HaveCount(6);
        parts[0].Should().Be("argon2id");
        parts[1].Should().Be("3");
        parts[2]
            .Should()
            .Be((64 * 1024).ToString(System.Globalization.CultureInfo.InvariantCulture));
        parts[3].Should().Be("4");
        FluentActions.Invoking(() => Convert.FromBase64String(parts[4])).Should().NotThrow();
        FluentActions.Invoking(() => Convert.FromBase64String(parts[5])).Should().NotThrow();
    }

    [Fact]
    public void HashSamePasswordTwiceProducesDistinctHashes()
    {
        sut.Hash("dup").Should().NotBe(sut.Hash("dup"));
    }

    [Theory]
    [InlineData("argon2id$3$65536$4$c2FsdA==")]
    [InlineData("argon2id$3$65536$4$c2FsdA==$aGFzaA==$extra")]
    [InlineData("bcrypt$3$65536$4$c2FsdA==$aGFzaA==")]
    [InlineData("argon2id$x$65536$4$c2FsdA==$aGFzaA==")]
    [InlineData("argon2id$3$x$4$c2FsdA==$aGFzaA==")]
    [InlineData("argon2id$3$65536$x$c2FsdA==$aGFzaA==")]
    [InlineData("argon2id$3$65536$4$!!!$aGFzaA==")]
    [InlineData("argon2id$3$65536$4$c2FsdA==$!!!")]
    public void VerifyMalformedHashStringReturnsFalse(string malformed)
    {
        sut.Verify("anything", malformed).Should().BeFalse();
    }
}
