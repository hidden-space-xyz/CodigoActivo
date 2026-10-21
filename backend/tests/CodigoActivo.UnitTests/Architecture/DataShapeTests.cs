using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class DataShapeTests
{
    private static readonly string[] SerializationNamespaces =
    [
        "System.Text.Json.Serialization",
        "System.Runtime.Serialization",
        "System.Xml.Serialization",
    ];

    private static readonly Type[] TableFilterCommands =
    [
        typeof(SendEmailToUsersCommand),
        typeof(SendEmailToEventAttendeesCommand),
    ];

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

    [Fact]
    public void DomainAssemblyTypesNeverCarrySerializationAttributes()
    {
        var attributes = typeof(Result)
            .Assembly.GetTypes()
            .SelectMany(IlReferences.TypesUsedBy)
            .SelectMany(IlReferences.Expand)
            .Where(type => type.IsSubclassOf(typeof(Attribute)))
            .Distinct()
            .ToList();

        var offenders = attributes
            .Where(attribute =>
                attribute.Namespace is { } ns
                && SerializationNamespaces.Any(prefix =>
                    ns.StartsWith(prefix, StringComparison.Ordinal)
                )
            )
            .Select(attribute => attribute.FullName)
            .ToList();

        attributes.Should().Contain(typeof(GeneratedRegexAttribute));
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void CommandsNeverCarryRawGuidsOutsideTableFilters()
    {
        var offenders = ProductionAssemblies
            .Application.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && type.GetInterfaces()
                    .Any(contract =>
                        contract.IsGenericType
                        && contract.GetGenericTypeDefinition() == typeof(ICommand<>)
                    )
            )
            .Except(TableFilterCommands)
            .SelectMany(command => RawGuidPaths(command, command.Name, []))
            .ToList();

        TableFilterCommands
            .Should()
            .OnlyContain(command => RawGuidPaths(command, command.Name, new HashSet<Type>()).Any());
        offenders.Should().BeEmpty();
    }

    private static IEnumerable<string> RawGuidPaths(Type type, string path, HashSet<Type> visited)
    {
        if (type == typeof(Guid))
        {
            return [path];
        }

        var members =
            type.Namespace?.StartsWith("CodigoActivo.", StringComparison.Ordinal) == true
            && !IsEntityId(type)
            && visited.Add(type)
                ? type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .SelectMany(property =>
                        RawGuidPaths(property.PropertyType, path + "." + property.Name, visited)
                    )
                : [];
        var elements = type.HasElementType ? [type.GetElementType()!] : type.GetGenericArguments();

        return members.Concat(elements.SelectMany(element => RawGuidPaths(element, path, visited)));
    }

    private static bool IsEntityId(Type type)
    {
        return type.GetInterfaces()
            .Any(contract =>
                contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEntityId<>)
            );
    }
}
