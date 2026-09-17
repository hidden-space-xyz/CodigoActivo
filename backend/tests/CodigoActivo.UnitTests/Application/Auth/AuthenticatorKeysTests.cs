using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Auth;

public sealed class AuthenticatorKeysTests
{
    [Theory]
    [InlineData("ABCDEFGH", "ABCD EFGH")]
    [InlineData("ABCDEFG", "ABCD EFG")]
    [InlineData("ABC", "ABC")]
    [InlineData("", "")]
    public void FormatSplitsTheSecretIntoGroupsOfFour(string secret, string expected)
    {
        AuthenticatorKeys.Format(secret).Should().Be(expected);
    }

    [Fact]
    public void BuildUriEncodesIssuerAndAccountAndUsesTheStandardProfile()
    {
        var uri = AuthenticatorKeys.BuildUri("Código Activo", "ana+x@test.com", "JBSWY3DP");

        uri.Should()
            .Be(
                "otpauth://totp/C%C3%B3digo%20Activo:ana%2Bx%40test.com?secret=JBSWY3DP&issuer=C%C3%B3digo%20Activo&algorithm=SHA1&digits=6&period=30"
            );
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed).Should().BeTrue();
        parsed!.Scheme.Should().Be("otpauth");
    }
}
