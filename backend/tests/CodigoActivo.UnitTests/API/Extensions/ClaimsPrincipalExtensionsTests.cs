using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using Xunit;

namespace CodigoActivo.UnitTests.API.Extensions;

public sealed class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    [Fact]
    public void GetUserId_ValidNameIdentifierClaim_ReturnsGuid()
    {
        var id = Guid.NewGuid();
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        principal.GetUserId().Should().Be(id);
    }

    [Fact]
    public void GetUserId_NameIdentifierClaimMissing_ReturnsNull()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Role, "whatever"));

        principal.GetUserId().Should().BeNull();
    }

    [Fact]
    public void GetUserId_NameIdentifierUnparseable_ReturnsNull()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        principal.GetUserId().Should().BeNull();
    }

    [Fact]
    public void IsAdmin_IsAdminClaimTrue_ReturnsTrue()
    {
        var principal = PrincipalWith(
            new Claim(ClaimsPrincipalExtensions.IsAdminClaim, bool.TrueString)
        );

        principal.IsAdmin().Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_IsAdminClaimFalse_ReturnsFalse()
    {
        var principal = PrincipalWith(
            new Claim(ClaimsPrincipalExtensions.IsAdminClaim, bool.FalseString)
        );

        principal.IsAdmin().Should().BeFalse();
    }

    [Fact]
    public void IsAdmin_IsAdminClaimAbsent_ReturnsFalse()
    {
        var principal = PrincipalWith(
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        );

        principal.IsAdmin().Should().BeFalse();
    }
}
