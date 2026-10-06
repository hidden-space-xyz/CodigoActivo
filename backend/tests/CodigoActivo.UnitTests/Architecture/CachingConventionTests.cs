using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.API.Caching;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Infrastructure.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class CachingConventionTests
{
    private static readonly string[] ReadMethods = ["GET", "HEAD"];
    private static readonly Type[] ApprovedCachedQueries =
    [
        typeof(ListUserTypesQuery),
        typeof(ListUserStatusTypesQuery),
        typeof(ListResourceTypesQuery),
        typeof(ListActivityRoleTypesQuery),
        typeof(ListActivityModalityTypesQuery),
        typeof(ListAssignmentStatusTypesQuery),
        typeof(GetDashboardSummaryQuery),
        typeof(GetDashboardAnalyticsQuery),
    ];

    [Fact]
    public void AnonymousReadActionsAlwaysDeclareOutputCacheBehavior()
    {
        var offenders = ControllerActions()
            .Where(IsReadAction)
            .Where(IsAnonymous)
            .Where(action => OutputCacheOf(action) is null)
            .Select(DisplayName)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void OutputCachedActionsAlwaysAreAnonymousReadsWithRegisteredPolicies()
    {
        var cachedActions = ControllerActions()
            .Select(action => new { Action = action, Cache = OutputCacheOf(action) })
            .Where(entry => entry.Cache is { NoStore: false })
            .ToList();

        var unsafeActions = cachedActions
            .Where(entry => !IsReadAction(entry.Action) || !IsAnonymous(entry.Action))
            .Select(entry => DisplayName(entry.Action))
            .ToList();
        var unknownPolicies = cachedActions
            .Where(entry => !IsRegisteredPolicy(entry.Cache!.PolicyName))
            .Select(entry => $"{DisplayName(entry.Action)} ({entry.Cache!.PolicyName})")
            .ToList();

        cachedActions.Should().NotBeEmpty();
        unsafeActions.Should().BeEmpty();
        unknownPolicies.Should().BeEmpty();
    }

    [Fact]
    public void CachedQueriesAlwaysMatchExplicitSafeSet()
    {
        var cached = typeof(ICachedQuery)
            .Assembly.GetTypes()
            .Where(type => type.IsClass && typeof(ICachedQuery).IsAssignableFrom(type))
            .ToList();

        cached.Should().BeEquivalentTo(ApprovedCachedQueries);
    }

    [Fact]
    public void HybridCacheIsOnlyUsedByTheCachingDecorator()
    {
        var consumers = new[] { typeof(ICachedQuery).Assembly, typeof(CachePolicies).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == typeof(HybridCache))
            )
            .ToList();

        consumers.Should().Equal(typeof(CachingQueryDecorator<,>));
    }

    private static IEnumerable<MethodInfo> ControllerActions()
    {
        return typeof(ApiControllerBase)
            .Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type =>
                type.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
            );
    }

    private static bool IsReadAction(MethodInfo action)
    {
        return action
            .GetCustomAttributes<HttpMethodAttribute>()
            .SelectMany(attribute => attribute.HttpMethods)
            .Any(method => ReadMethods.Contains(method, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsAnonymous(MethodInfo action)
    {
        return action.IsDefined(typeof(AllowAnonymousAttribute), inherit: true)
            || action.DeclaringType!.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
    }

    private static OutputCacheAttribute? OutputCacheOf(MethodInfo action)
    {
        return action.GetCustomAttribute<OutputCacheAttribute>(inherit: true)
            ?? action.DeclaringType!.GetCustomAttribute<OutputCacheAttribute>(inherit: true);
    }

    private static bool IsRegisteredPolicy(string? policy)
    {
        return policy is not null
            && (
                CacheTags.OutputCached.Contains(policy, StringComparer.Ordinal)
                || string.Equals(policy, OutputCachePolicies.Seo, StringComparison.Ordinal)
            );
    }

    private static string DisplayName(MethodInfo action)
    {
        return $"{action.DeclaringType!.Name}.{action.Name}";
    }
}
