using System.Text.RegularExpressions;
using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Auth;

public sealed partial class AccountTokensTests
{
    [Fact]
    public void CreateReturnsSixtyFourLowercaseHexCharacters()
    {
        var token = AccountTokens.Create();

        token.Should().HaveLength(64);
        LowercaseHexPattern().IsMatch(token).Should().BeTrue();
    }

    [Fact]
    public void CreateTwoCallsReturnDifferentTokens()
    {
        var first = AccountTokens.Create();
        var second = AccountTokens.Create();

        first.Should().NotBe(second);
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex LowercaseHexPattern();
}
