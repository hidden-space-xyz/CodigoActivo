using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using Xunit;

namespace CodigoActivo.UnitTests.API.Extensions;

public sealed class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims)
    {
        return new(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    [Fact]
    public void GetUserIdValidNameIdentifierClaimReturnsGuid()
    {
        var id = Guid.NewGuid();
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        principal.GetUserId().Should().Be(id);
    }

    [Fact]
    public void GetUserIdNameIdentifierClaimMissingReturnsNull()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Role, "whatever"));

        principal.GetUserId().Should().BeNull();
    }

    [Fact]
    public void GetUserIdNameIdentifierUnparseableReturnsNull()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        principal.GetUserId().Should().BeNull();
    }

    [Fact]
    public void IsAdminIsAdminClaimTrueReturnsTrue()
    {
        var principal = PrincipalWith(
            new Claim(ClaimsPrincipalExtensions.IsAdminClaim, bool.TrueString)
        );

        principal.IsAdmin().Should().BeTrue();
    }

    [Fact]
    public void IsAdminIsAdminClaimFalseReturnsFalse()
    {
        var principal = PrincipalWith(
            new Claim(ClaimsPrincipalExtensions.IsAdminClaim, bool.FalseString)
        );

        principal.IsAdmin().Should().BeFalse();
    }

    [Fact]
    public void IsAdminIsAdminClaimAbsentReturnsFalse()
    {
        var principal = PrincipalWith(
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        );

        principal.IsAdmin().Should().BeFalse();
    }
}
