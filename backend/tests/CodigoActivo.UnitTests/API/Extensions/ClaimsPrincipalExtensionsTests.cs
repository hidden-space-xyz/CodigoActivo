using System.Security.Claims;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Constants;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.API.Extensions;

public sealed class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    // ---- GetUserId ---------------------------------------------------------

    [Fact]
    public void GetUserId_returns_guid_when_name_identifier_is_valid()
    {
        var id = Guid.NewGuid();
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        principal.GetUserId().Should().Be(id);
    }

    [Fact]
    public void GetUserId_returns_null_when_name_identifier_missing()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Role, "whatever"));

        principal.GetUserId().Should().BeNull();
    }

    [Fact]
    public void GetUserId_returns_null_when_name_identifier_unparseable()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        principal.GetUserId().Should().BeNull();
    }

    // ---- IsAdmin -----------------------------------------------------------

    [Fact]
    public void IsAdmin_is_true_when_role_matches_admin_user_type()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Role, SeedIds.UserTypes.Admin.ToString()));

        principal.IsAdmin().Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_is_false_for_non_admin_role()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Role, SeedIds.UserTypes.Member.ToString()));

        principal.IsAdmin().Should().BeFalse();
    }

    [Fact]
    public void IsAdmin_is_false_when_no_role_claim_present()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        principal.IsAdmin().Should().BeFalse();
    }
}
