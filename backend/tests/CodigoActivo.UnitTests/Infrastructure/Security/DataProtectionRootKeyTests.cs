using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class DataProtectionRootKeyTests
{
    private const string SequentialRootKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    [Fact]
    public void DeriveKeyKnownRootKeyReturnsHkdfSha256OfThePurpose()
    {
        var key = DataProtectionRootKey
            .Parse(SequentialRootKey)
            .DeriveKey("CodigoActivo.Tests.v1", 32);

        Convert
            .ToHexString(key)
            .Should()
            .Be("FDC2193C0E3E7B67CBBE23D6C3C7AC3EBD0F9D621C304EFBC1A6A6F30C03FDD2");
    }

    [Fact]
    public void DeriveKeyOtherPurposeOrRootKeyReturnsAnIndependentKey()
    {
        var rootKey = DataProtectionRootKey.Parse(SequentialRootKey);
        var key = rootKey.DeriveKey("first-purpose", 32);

        rootKey.DeriveKey("first-purpose", 32).Should().Equal(key);
        rootKey.DeriveKey("second-purpose", 32).Should().NotEqual(key);
        DataProtectionRootKeys.Random().DeriveKey("first-purpose", 32).Should().NotEqual(key);
        key.Should().NotEqual(Convert.FromBase64String(SequentialRootKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void DeriveKeyBlankPurposeThrows(string purpose)
    {
        var rootKey = DataProtectionRootKey.Parse(SequentialRootKey);

        var act = () => rootKey.DeriveKey(purpose, 32);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(SequentialRootKey)]
    [InlineData(SequentialRootKey + "\r\n")]
    public void TryParseThirtyTwoBytesInBase64ReturnsTheRootKey(string encoded)
    {
        DataProtectionRootKey.TryParse(encoded, out var rootKey).Should().BeTrue();

        rootKey.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a-separate-strong-data-protection-password")]
    [InlineData("BwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcHBw==")]
    [InlineData("BwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcHBwcH")]
    [InlineData("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWYwMTIzNDU2Nzg5YWJjZGVm")]
    [InlineData("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void TryParseAnythingButThirtyTwoRandomBytesInBase64ReturnsFalse(string? encoded)
    {
        DataProtectionRootKey.TryParse(encoded, out var rootKey).Should().BeFalse();

        rootKey.Should().BeNull();
        FluentActions
            .Invoking(() => DataProtectionRootKey.Parse(encoded))
            .Should()
            .Throw<FormatException>();
    }
}
