using System.Buffers.Text;
using AwesomeAssertions;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class DataProtectionUnsubscribeTokensTests
{
    private readonly EphemeralDataProtectionProvider provider = new();

    [Fact]
    public void TryReadIssuedTokenReturnsTheAccountItWasIssuedFor()
    {
        var sut = new DataProtectionUnsubscribeTokens(provider);
        var userId = UserId.New();

        var token = sut.Issue(userId);

        token.Should().MatchRegex("^[A-Za-z0-9_-]+$", "the token travels in a URL");
        sut.TryRead(token, out var read).Should().BeTrue();
        read.Should().Be(userId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA")]
    public void TryReadTokenNotIssuedHereReturnsFalse(string token)
    {
        var sut = new DataProtectionUnsubscribeTokens(provider);

        sut.TryRead(token, out var read).Should().BeFalse();
        read.Should().Be(default(UserId));
    }

    [Fact]
    public void TryReadAlteredTokenReturnsFalse()
    {
        var sut = new DataProtectionUnsubscribeTokens(provider);
        var token = sut.Issue(UserId.New());
        var altered = token[..^2] + (token[^2] == 'A' ? 'B' : 'A') + token[^1];

        sut.TryRead(altered, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadTokenOfAnotherPurposeReturnsFalse()
    {
        var sut = new DataProtectionUnsubscribeTokens(provider);
        var foreign = Base64Url.EncodeToString(
            provider.CreateProtector("CodigoActivo.Other").Protect(Guid.NewGuid().ToByteArray())
        );

        sut.TryRead(foreign, out _).Should().BeFalse();
    }
}
