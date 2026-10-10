using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class HmacOneTimeCodeHasherTests
{
    private readonly byte[] key = RandomNumberGenerator.GetBytes(HmacOneTimeCodeHasher.KeySize);
    private readonly HmacOneTimeCodeHasher sut;

    public HmacOneTimeCodeHasherTests()
    {
        sut = new HmacOneTimeCodeHasher(key);
    }

    [Fact]
    public void VerifySameCodeAsHashReturnsTrue()
    {
        var hash = sut.Hash("123456");

        sut.Verify("123456", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyWrongCodeReturnsFalse()
    {
        var hash = sut.Hash("123456");

        sut.Verify("123457", hash).Should().BeFalse();
    }

    [Fact]
    public void HashAnyCodeProducesTheBase64HmacSha256UnderTheKey()
    {
        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("a-link-token"));

        sut.Hash("a-link-token").Should().Be(Convert.ToBase64String(expected));
    }

    [Fact]
    public void VerifyHashFromAnotherKeyReturnsFalse()
    {
        var other = new HmacOneTimeCodeHasher(
            RandomNumberGenerator.GetBytes(HmacOneTimeCodeHasher.KeySize)
        );

        sut.Verify("123456", other.Hash("123456")).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("!!!")]
    [InlineData("fake:123456")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
    public void VerifyMalformedHashReturnsFalse(string malformed)
    {
        sut.Verify("123456", malformed).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void ConstructorKeyOfWrongLengthThrows(int length)
    {
        var act = () => new HmacOneTimeCodeHasher(new byte[length]);

        act.Should().Throw<ArgumentException>();
    }
}
