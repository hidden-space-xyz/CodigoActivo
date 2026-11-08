using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace CodigoActivo.ArchitectureTests;

public sealed class ApiConventionTests
{
    [Fact]
    public void ControllersAlwaysDeriveFromTheApiBaseInTheirFeature()
    {
        var controllers = ProductionAssemblies.Controllers.ToList();

        var offenders = controllers
            .Where(type =>
                type.BaseType != typeof(ApiControllerBase)
                || !type.Name.EndsWith("Controller", StringComparison.Ordinal)
                || type.Namespace?.Split('.') is not ["CodigoActivo", "API", _]
            )
            .Select(type => type.FullName)
            .ToList();

        controllers.Should().HaveCountGreaterThan(15);
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void ControllerActionsAlwaysDeclareTheirHttpMethod()
    {
        var actions = ProductionAssemblies
            .ControllerActions.Where(method =>
                !method.IsSpecialName
                && !method.IsDefined(typeof(NonActionAttribute), inherit: true)
            )
            .ToList();

        var offenders = actions
            .Where(action => !action.IsDefined(typeof(HttpMethodAttribute), inherit: true))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .ToList();

        actions.Should().HaveCountGreaterThan(100);
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void RequestBodiesAlwaysAreRequestContractsOfAnApiFeature()
    {
        var requests = ProductionAssemblies
            .Api.GetTypes()
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal))
            .ToList();
        var bodies = ProductionAssemblies
            .ControllerActions.SelectMany(action => action.GetParameters())
            .Where(parameter => parameter.IsDefined(typeof(FromBodyAttribute), inherit: true))
            .Select(parameter => parameter.ParameterType)
            .ToList();

        var offenders = requests
            .Concat(bodies)
            .Distinct()
            .Where(type =>
                !type.Name.EndsWith("Request", StringComparison.Ordinal)
                || type.Namespace?.Split('.') is not ["CodigoActivo", "API", _, "Contracts"]
            )
            .Select(type => type.FullName)
            .ToList();

        bodies.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void OnlyTheCredentialEndpointsThatRunArgon2WaitForThePasswordHashingPool()
    {
        var actions = ProductionAssemblies.ControllerActions.ToList();

        var hashing = actions
            .Where(action =>
                action
                    .GetParameters()
                    .Any(parameter => HashesPasswords(parameter.ParameterType, []))
            )
            .Select(ActionName)
            .ToList();
        var marked = actions
            .Where(action => action.IsDefined(typeof(PasswordHashingAttribute), inherit: false))
            .ToList();
        var outsideCredentials = marked
            .Where(action =>
                action.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName
                    is not SecurityPolicies.Credentials
            )
            .Select(ActionName)
            .ToList();

        hashing.Should().HaveCountGreaterThan(10);
        marked.Select(ActionName).Should().BeEquivalentTo(hashing);
        outsideCredentials.Should().BeEmpty();
    }

    private static string ActionName(MethodInfo action)
    {
        return $"{action.DeclaringType!.Name}.{action.Name}";
    }

    private static bool HashesPasswords(Type type, HashSet<Type> visited)
    {
        if (type == typeof(IPasswordHasher))
        {
            return true;
        }

        if (!visited.Add(type))
        {
            return false;
        }

        return ImplementationsOf(type)
            .SelectMany(implementation => implementation.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => HashesPasswords(parameter.ParameterType, visited));
    }

    private static IEnumerable<Type> ImplementationsOf(Type type)
    {
        if (type.IsInterface && type.IsConstructedGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            return
                definition == typeof(ICommandHandler<,>) || definition == typeof(IQueryHandler<,>)
                ? ProductionAssemblies
                    .Application.GetTypes()
                    .Where(candidate => !candidate.IsAbstract && type.IsAssignableFrom(candidate))
                : [];
        }

        return type.IsClass && !type.IsAbstract && type.Assembly == ProductionAssemblies.Application
            ? [type]
            : [];
    }
}
