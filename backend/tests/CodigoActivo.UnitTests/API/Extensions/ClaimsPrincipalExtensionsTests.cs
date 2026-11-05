using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
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

    [Theory]
    [InlineData(StaffRole.None, new string[0])]
    [InlineData(StaffRole.Editor, new[] { ClaimsPrincipalExtensions.EditorRole })]
    [InlineData(
        StaffRole.Administrator,
        new[] { ClaimsPrincipalExtensions.EditorRole, ClaimsPrincipalExtensions.AdministratorRole }
    )]
    public void RolesOfIncludesTheRolesTheStaffRoleIncludes(StaffRole staffRole, string[] expected)
    {
        ClaimsPrincipalExtensions.RolesOf(staffRole).Should().Equal(expected);
    }

    [Fact]
    public void RoleOfNoStaffRoleThrows()
    {
        var act = () => ClaimsPrincipalExtensions.RoleOf(StaffRole.None);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(StaffRole.None, false, false)]
    [InlineData(StaffRole.Editor, true, false)]
    [InlineData(StaffRole.Administrator, true, true)]
    public void HasStaffRoleHoldsTheRolesThatTheClaimsInclude(
        StaffRole held,
        bool editor,
        bool administrator
    )
    {
        var principal = PrincipalWith([
            .. ClaimsPrincipalExtensions
                .RolesOf(held)
                .Select(role => new Claim(ClaimTypes.Role, role)),
        ]);

        principal.HasStaffRole(StaffRole.None).Should().BeTrue();
        principal.HasStaffRole(StaffRole.Editor).Should().Be(editor);
        principal.HasStaffRole(StaffRole.Administrator).Should().Be(administrator);
    }

    [Fact]
    public void IsSuperAdministratorIsTheInitialAdministratorOnly()
    {
        PrincipalWith(
                new Claim(ClaimTypes.NameIdentifier, KnownIds.Users.InitialAdministrator.ToString())
            )
            .IsSuperAdministrator()
            .Should()
            .BeTrue();
        PrincipalWith(
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, ClaimsPrincipalExtensions.AdministratorRole)
            )
            .IsSuperAdministrator()
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsPrivacyOfficerReadsTheOfficerRoleClaim()
    {
        PrincipalWith(new Claim(ClaimTypes.Role, ClaimsPrincipalExtensions.PrivacyOfficerRole))
            .IsPrivacyOfficer()
            .Should()
            .BeTrue();
        PrincipalWith(new Claim(ClaimTypes.Role, ClaimsPrincipalExtensions.AdministratorRole))
            .IsPrivacyOfficer()
            .Should()
            .BeFalse();
    }
}
