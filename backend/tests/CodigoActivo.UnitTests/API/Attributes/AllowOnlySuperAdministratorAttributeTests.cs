using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace CodigoActivo.UnitTests.API.Attributes;

public sealed class AllowOnlySuperAdministratorAttributeTests
{
    private static AuthorizationFilterContext BuildContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private static ClaimsPrincipal User(Guid id, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public void OnAuthorizationNoUserIdChallenges()
    {
        var context = BuildContext(new ClaimsPrincipal(new ClaimsIdentity()));

        new AllowOnlySuperAdministratorAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<ChallengeResult>();
    }

    [Fact]
    public void OnAuthorizationInitialAdministratorAllowsRequest()
    {
        var context = BuildContext(User(KnownIds.Users.InitialAdministrator));

        new AllowOnlySuperAdministratorAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnAuthorizationAnotherAdministratorForbids()
    {
        var context = BuildContext(
            User(
                Guid.NewGuid(),
                ClaimsPrincipalExtensions.EditorRole,
                ClaimsPrincipalExtensions.AdministratorRole
            )
        );

        new AllowOnlySuperAdministratorAttribute { PrivacyOfficerAllowed = true }.OnAuthorization(
            context
        );

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void OnAuthorizationPrivacyOfficerPassesOnlyWhereAllowed()
    {
        var officer = User(Guid.NewGuid(), ClaimsPrincipalExtensions.PrivacyOfficerRole);
        var refused = BuildContext(officer);
        var allowed = BuildContext(officer);

        new AllowOnlySuperAdministratorAttribute().OnAuthorization(refused);
        new AllowOnlySuperAdministratorAttribute { PrivacyOfficerAllowed = true }.OnAuthorization(
            allowed
        );

        refused.Result.Should().BeOfType<ForbidResult>();
        allowed.Result.Should().BeNull();
    }
}
