using System.Security.Claims;
using CodigoActivo.API.Attributes;
using CodigoActivo.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.API.Attributes;

public sealed class AllowOnlyAdminAttributeTests
{
    private static readonly string AdminRole = SeedIds.UserTypes.Admin.ToString();

    private static AuthorizationFilterContext BuildContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor()
        );
        return new AuthorizationFilterContext(actionContext, []);
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal User(Guid id, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public void OnAuthorization_challenges_when_no_user_id()
    {
        var context = BuildContext(Anonymous());

        new AllowOnlyAdminAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<ChallengeResult>();
    }

    [Fact]
    public void OnAuthorization_forbids_authenticated_non_admin()
    {
        var context = BuildContext(User(Guid.NewGuid(), SeedIds.UserTypes.Member.ToString()));

        new AllowOnlyAdminAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void OnAuthorization_allows_admin()
    {
        var context = BuildContext(User(Guid.NewGuid(), AdminRole));

        new AllowOnlyAdminAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }
}
