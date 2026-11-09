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
    public async Task OnAuthorizationAsyncNoUserIdChallenges()
    {
        var context = BuildContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await new AllowOnlySuperAdministratorAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ChallengeResult>();
    }

    [Fact]
    public async Task OnAuthorizationAsyncInitialAdministratorAllowsRequest()
    {
        var context = BuildContext(User(KnownIds.Users.InitialAdministrator));

        await new AllowOnlySuperAdministratorAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnAuthorizationAsyncAnotherAdministratorForbids()
    {
        var context = BuildContext(
            User(
                Guid.NewGuid(),
                ClaimsPrincipalExtensions.EditorRole,
                ClaimsPrincipalExtensions.AdministratorRole
            )
        );

        await new AllowOnlySuperAdministratorAttribute
        {
            PrivacyOfficerAllowed = true,
        }.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task OnAuthorizationAsyncPrivacyOfficerPassesOnlyWhereAllowed()
    {
        var officer = User(Guid.NewGuid(), ClaimsPrincipalExtensions.PrivacyOfficerRole);
        var refused = BuildContext(officer);
        var allowed = BuildContext(officer);

        await new AllowOnlySuperAdministratorAttribute().OnAuthorizationAsync(refused);
        await new AllowOnlySuperAdministratorAttribute
        {
            PrivacyOfficerAllowed = true,
        }.OnAuthorizationAsync(allowed);

        refused.Result.Should().BeOfType<ForbidResult>();
        allowed.Result.Should().BeNull();
    }
}
