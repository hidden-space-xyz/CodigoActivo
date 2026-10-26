using AwesomeAssertions;
using CodigoActivo.API.Controllers.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
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
}
