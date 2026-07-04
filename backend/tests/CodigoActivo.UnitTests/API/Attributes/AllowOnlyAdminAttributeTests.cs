using System.Security.Claims;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
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

    private static ClaimsPrincipal User(Guid id, bool isAdmin = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()) };
        if (isAdmin) claims.Add(new Claim(ClaimsPrincipalExtensions.IsAdminClaim, bool.TrueString));
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
        var context = BuildContext(User(Guid.NewGuid()));

        new AllowOnlyAdminAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void OnAuthorization_allows_admin()
    {
        var context = BuildContext(User(Guid.NewGuid(), isAdmin: true));

        new AllowOnlyAdminAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }
}
