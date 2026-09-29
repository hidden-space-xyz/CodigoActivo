using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class DataShapeTests
{
    [Fact]
    public void ApplicationContractsPublicTypesAreWireRecordsOrBindingModels()
    {
        var contractTypes = typeof(IQuery<>)
            .Assembly.GetTypes()
            .Where(type => type.IsPublic && IsContractNamespace(type.Namespace))
            .ToList();

        var offenders = contractTypes
            .Where(type => !type.IsEnum && !IsWireRecord(type) && !IsBindingModel(type))
            .Select(type => type.Name)
            .ToList();

        contractTypes.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void ApplicationWireRecordsAlwaysLiveInFeatureContracts()
    {
        var offenders = typeof(IQuery<>)
            .Assembly.GetTypes()
            .Where(type =>
                type.IsPublic && IsWireRecord(type) && !IsContractNamespace(type.Namespace)
            )
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty();
    }

    private static bool IsContractNamespace(string? ns)
    {
        return ns is not null
            && ns.StartsWith("CodigoActivo.Application.", StringComparison.Ordinal)
            && ns.EndsWith(".Contracts", StringComparison.Ordinal);
    }

    private static bool IsWireRecord(Type type)
    {
        return type.GetMethod("<Clone>$") is not null
            && (
                type.Name.EndsWith("Request", StringComparison.Ordinal)
                || type.Name.EndsWith("Response", StringComparison.Ordinal)
            )
            && !type.GetProperties()
                .Any(property => typeof(Stream).IsAssignableFrom(property.PropertyType));
    }

    private static bool IsBindingModel(Type type)
    {
        return type is { IsClass: true, IsAbstract: false }
            && type.GetMethod("<Clone>$") is null
            && type.Name.EndsWith("Query", StringComparison.Ordinal);
    }

    [Fact]
    public void DomainAssemblyTypesNeverCarryConfigurationOptions()
    {
        var offenders = typeof(Result)
            .Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Options", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }
}
