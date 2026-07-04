using System.Linq.Expressions;
using System.Security.Claims;
using CodigoActivo.API.Attributes;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.API.Attributes;

public sealed class AllowOnlySelfAttributeTests
{
    private static readonly string AdminRole = SeedIds.UserTypes.Admin.ToString();

    private readonly IUserRepository users = Substitute.For<IUserRepository>();

    private AuthorizationFilterContext BuildContext(
        ClaimsPrincipal principal,
        object? routeUserId = null,
        bool includeRouteKey = true
    )
    {
        var httpContext = new DefaultHttpContext
        {
            User = principal,
            RequestServices = new ServiceCollection()
                .AddSingleton(users)
                .BuildServiceProvider(),
        };
        var routeData = new RouteData();
        if (includeRouteKey)
        {
            routeData.Values["userId"] = routeUserId;
        }

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
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
    public async Task OnAuthorizationAsync_challenges_when_no_user_id()
    {
        var context = BuildContext(Anonymous(), Guid.NewGuid());

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ChallengeResult>();
        await users.DidNotReceiveWithAnyArgs().ExistsAsync(default!, default);
    }

    [Fact]
    public async Task OnAuthorizationAsync_allows_admin_regardless_of_route()
    {
        var context = BuildContext(User(Guid.NewGuid(), AdminRole), Guid.NewGuid());

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        await users.DidNotReceiveWithAnyArgs().ExistsAsync(default!, default);
    }

    [Fact]
    public async Task OnAuthorizationAsync_allows_when_route_user_is_self()
    {
        var self = Guid.NewGuid();
        var context = BuildContext(User(self), self);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        await users.DidNotReceiveWithAnyArgs().ExistsAsync(default!, default);
    }

    [Fact]
    public async Task OnAuthorizationAsync_forbids_when_route_key_missing()
    {
        var context = BuildContext(User(Guid.NewGuid()), includeRouteKey: false);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        await users.DidNotReceiveWithAnyArgs().ExistsAsync(default!, default);
    }

    [Fact]
    public async Task OnAuthorizationAsync_forbids_when_route_value_unparseable()
    {
        var context = BuildContext(User(Guid.NewGuid()), "not-a-guid");

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        await users.DidNotReceiveWithAnyArgs().ExistsAsync(default!, default);
    }

    [Fact]
    public async Task OnAuthorizationAsync_allows_when_target_is_own_child()
    {
        var currentUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        users
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var context = BuildContext(User(currentUserId), childId);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        await users.Received(1)
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAuthorizationAsync_forbids_when_target_is_unrelated_user()
    {
        users
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var context = BuildContext(User(Guid.NewGuid()), Guid.NewGuid());

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        await users.Received(1)
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>());
    }
}
