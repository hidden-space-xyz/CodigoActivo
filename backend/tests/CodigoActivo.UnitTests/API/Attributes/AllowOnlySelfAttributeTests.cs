using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.API.Attributes;

public sealed class AllowOnlySelfAttributeTests : IDisposable
{
    private readonly FakeReadStore store = new();
    private readonly ServiceProvider services;

    public AllowOnlySelfAttributeTests()
    {
        services = new ServiceCollection()
            .AddSingleton<IQueryHandler<IsGuardianOfQuery, bool>>(
                new IsGuardianOfQueryHandler(store, new FakeQueryExecutor())
            )
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        services.Dispose();
    }

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

    private static ClaimsPrincipal Anonymous()
    {
        return new(new ClaimsIdentity());
    }

    private static ClaimsPrincipal User(Guid id, bool isAdmin = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()) };
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.IsAdminClaim, bool.TrueString));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static UserRow Dependent(Guid id, Guid parentId)
    {
        return new()
        {
            LastName = "Apellido",
            FirstName = "Nombre",
            Id = id,
            ParentId = parentId,
        };
    }

    [Fact]
    public async Task OnAuthorizationAsyncAnonymousUserChallenges()
    {
        var context = BuildContext(Anonymous(), Guid.NewGuid());

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ChallengeResult>();
        store.ReadsOf<UserRow>().Should().Be(0);
    }

    [Fact]
    public async Task OnAuthorizationAsyncAdminUserAllowsRegardlessOfRoute()
    {
        var context = BuildContext(User(Guid.NewGuid(), isAdmin: true), Guid.NewGuid());

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        store.ReadsOf<UserRow>().Should().Be(0);
    }

    [Fact]
    public async Task OnAuthorizationAsyncRouteUserIsSelfAllows()
    {
        var self = Guid.NewGuid();
        var context = BuildContext(User(self), self);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        store.ReadsOf<UserRow>().Should().Be(0);
    }

    [Fact]
    public async Task OnAuthorizationAsyncRouteKeyMissingForbids()
    {
        var context = BuildContext(User(Guid.NewGuid()), includeRouteKey: false);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        store.ReadsOf<UserRow>().Should().Be(0);
    }

    [Fact]
    public async Task OnAuthorizationAsyncRouteValueUnparseableForbids()
    {
        var context = BuildContext(User(Guid.NewGuid()), "not-a-guid");

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        store.ReadsOf<UserRow>().Should().Be(0);
    }

    [Fact]
    public async Task OnAuthorizationAsyncTargetIsOwnChildAllows()
    {
        var currentUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        store.Users.Add(Dependent(childId, currentUserId));
        var context = BuildContext(User(currentUserId), childId);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        store.ReadsOf<UserRow>().Should().Be(1);
    }

    [Fact]
    public async Task OnAuthorizationAsyncTargetIsUnrelatedUserForbids()
    {
        store.Users.Add(Dependent(Guid.NewGuid(), Guid.NewGuid()));
        var context = BuildContext(User(Guid.NewGuid()), Guid.NewGuid());

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>();
        store.ReadsOf<UserRow>().Should().Be(1);
    }

    [Fact]
    public async Task OnAuthorizationAsyncTargetIsOwnChildQueriesForChildOwnedByCaller()
    {
        var callerId = Guid.NewGuid();
        var ownChildId = Guid.NewGuid();
        var foreignChildId = Guid.NewGuid();
        store.Users.AddRange([
            Dependent(ownChildId, callerId),
            Dependent(foreignChildId, Guid.NewGuid()),
        ]);
        var ownChild = BuildContext(User(callerId), ownChildId);
        var foreignChild = BuildContext(User(callerId), foreignChildId);

        await new AllowOnlySelfAttribute().OnAuthorizationAsync(ownChild);
        await new AllowOnlySelfAttribute().OnAuthorizationAsync(foreignChild);

        ownChild.Result.Should().BeNull();
        foreignChild.Result.Should().BeOfType<ForbidResult>();
    }
}
