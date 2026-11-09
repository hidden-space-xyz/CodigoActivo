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
    public async Task OnAuthorizationAsyncNoUserIdChallenges()
    {
        var context = BuildContext(Anonymous());

        await new AllowOnlyStaffAttribute(StaffRole.Editor).OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ChallengeResult>();
    }

    [Theory]
    [InlineData(StaffRole.Editor)]
    [InlineData(StaffRole.Administrator)]
    public async Task OnAuthorizationAsyncUserWithoutStaffRoleForbids(StaffRole staffRole)
    {
        var context = BuildContext(User());

        await new AllowOnlyStaffAttribute(staffRole).OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task OnAuthorizationAsyncEditorOnAnAdministratorTargetForbids()
    {
        var context = BuildContext(User(ClaimsPrincipalExtensions.EditorRole));

        await new AllowOnlyStaffAttribute(StaffRole.Administrator).OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData(StaffRole.Editor)]
    [InlineData(StaffRole.Administrator)]
    public async Task OnAuthorizationAsyncAdministratorAllowsEveryStaffTarget(StaffRole staffRole)
    {
        var context = BuildContext(
            User(ClaimsPrincipalExtensions.EditorRole, ClaimsPrincipalExtensions.AdministratorRole)
        );

        await new AllowOnlyStaffAttribute(staffRole).OnAuthorizationAsync(context);

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
