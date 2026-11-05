using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace CodigoActivo.UnitTests.API.Attributes;

public sealed class AllowOnlyStaffAttributeTests
{
    private static AuthorizationFilterContext BuildContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private static ClaimsPrincipal Anonymous()
    {
        return new(new ClaimsIdentity());
    }

    private static ClaimsPrincipal User(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public void OnAuthorizationNoUserIdChallenges()
    {
        var context = BuildContext(Anonymous());

        new AllowOnlyStaffAttribute(StaffRole.Editor).OnAuthorization(context);

        context.Result.Should().BeOfType<ChallengeResult>();
    }

    [Theory]
    [InlineData(StaffRole.Editor)]
    [InlineData(StaffRole.Administrator)]
    public void OnAuthorizationUserWithoutStaffRoleForbids(StaffRole staffRole)
    {
        var context = BuildContext(User());

        new AllowOnlyStaffAttribute(staffRole).OnAuthorization(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void OnAuthorizationEditorOnAnAdministratorTargetForbids()
    {
        var context = BuildContext(User(ClaimsPrincipalExtensions.EditorRole));

        new AllowOnlyStaffAttribute(StaffRole.Administrator).OnAuthorization(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData(StaffRole.Editor)]
    [InlineData(StaffRole.Administrator)]
    public void OnAuthorizationAdministratorAllowsEveryStaffTarget(StaffRole staffRole)
    {
        var context = BuildContext(
            User(ClaimsPrincipalExtensions.EditorRole, ClaimsPrincipalExtensions.AdministratorRole)
        );

        new AllowOnlyStaffAttribute(staffRole).OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void RolesRequireTheClaimOfTheStaffRole()
    {
        new AllowOnlyStaffAttribute(StaffRole.Editor)
            .Roles.Should()
            .Be(ClaimsPrincipalExtensions.EditorRole);
        new AllowOnlyStaffAttribute(StaffRole.Administrator)
            .Roles.Should()
            .Be(ClaimsPrincipalExtensions.AdministratorRole);
    }
}
