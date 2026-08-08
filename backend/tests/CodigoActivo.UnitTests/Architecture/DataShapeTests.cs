using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class DataShapeTests
{
    [Fact]
    public void ApplicationDtos_PublicTypes_AreWireRecordsOnly()
    {
        var dtoTypes = typeof(IQuery<>)
            .Assembly.GetTypes()
            .Where(type =>
                type.IsPublic
                && string.Equals(
                    type.Namespace,
                    "CodigoActivo.Application.DTOs",
                    StringComparison.Ordinal
                )
            )
            .ToList();

        var offenders = dtoTypes
            .Where(type =>
                type.GetMethod("<Clone>$") is null
                || !(
                    type.Name.EndsWith("Request", StringComparison.Ordinal)
                    || type.Name.EndsWith("Response", StringComparison.Ordinal)
                )
                || type.GetProperties()
                    .Any(property => typeof(Stream).IsAssignableFrom(property.PropertyType))
            )
            .Select(type => type.Name)
            .ToList();

        dtoTypes.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void DomainAssembly_Types_NeverCarryConfigurationOptions()
    {
        var offenders = typeof(Result)
            .Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Options", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }
}
