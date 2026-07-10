using System.Linq.Expressions;
using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.API.Attributes;

public sealed class AllowOnlySelfAttributeTests : IDisposable
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly ServiceProvider services;

    public AllowOnlySelfAttributeTests()
    {
        services = new ServiceCollection().AddSingleton(users).BuildServiceProvider();
    }

    public void Dispose() => services.Dispose();

    private AuthorizationFilterContext BuildContext(
        ClaimsPrincipal principal,
        object? routeUserId = null,
        bool includeRouteKey = true
    )
    {
        var httpContext = new DefaultHttpContext { User = principal, RequestServices = services };
        var routeData = new RouteData();
        if (includeRouteKey)
        {
            routeData.Values["userId"] = routeUserId;
        }

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal User(Guid id, bool isAdmin = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()) };
        if (isAdmin)
            claims.Add(new Claim(ClaimsPrincipalExtensions.IsAdminClaim, bool.TrueString));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public async Task OnAuthorizationAsync_AnonymousUser_Challenges()
    {
        var context = BuildContext(Anonymous(), Guid.NewGuid());

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ChallengeResult>();
        await users
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnAuthorizationAsync_AdminUser_AllowsRegardlessOfRoute()
    {
        var context = BuildContext(User(Guid.NewGuid(), isAdmin: true), Guid.NewGuid());

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        await users
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnAuthorizationAsync_RouteUserIsSelf_Allows()
    {
        var self = Guid.NewGuid();
        var context = BuildContext(User(self), self);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        await users
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnAuthorizationAsync_RouteKeyMissing_Forbids()
    {
        var context = BuildContext(User(Guid.NewGuid()), includeRouteKey: false);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        await users
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnAuthorizationAsync_RouteValueUnparseable_Forbids()
    {
        var context = BuildContext(User(Guid.NewGuid()), "not-a-guid");

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        await users
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnAuthorizationAsync_TargetIsOwnChild_Allows()
    {
        var currentUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        users
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var context = BuildContext(User(currentUserId), childId);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        await users
            .Received(1)
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAuthorizationAsync_TargetIsUnrelatedUser_Forbids()
    {
        users
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var context = BuildContext(User(Guid.NewGuid()), Guid.NewGuid());

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        await users
            .Received(1)
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAuthorizationAsync_TargetIsOwnChild_QueriesForChildOwnedByCaller()
    {
        var callerId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        Expression<Func<User, bool>>? captured = null;
        users
            .ExistsAsync(
                Arg.Do<Expression<Func<User, bool>>>(predicate => captured = predicate),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
        var context = BuildContext(User(callerId), childId);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        captured.Should().NotBeNull();
        var accepts = captured!.Compile();
        accepts(new User { Id = childId, ParentId = callerId }).Should().BeTrue();
        accepts(new User { Id = childId, ParentId = Guid.NewGuid() }).Should().BeFalse();
    }
}
