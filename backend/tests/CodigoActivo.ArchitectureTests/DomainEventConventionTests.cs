using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.ArchitectureTests;

public sealed class DomainEventConventionTests
{
    [Fact]
    public void DomainEventsAlwaysAreSealedRecordsDeclaredInDomain()
    {
        var events = ProductionAssemblies
            .AllTypes.Where(type =>
                type is { IsInterface: false, IsAbstract: false }
                && typeof(IDomainEvent).IsAssignableFrom(type)
            )
            .ToList();

        var offenders = events
            .Where(type =>
                type.Assembly != ProductionAssemblies.Domain
                || !type.IsSealed
                || type.GetMethod("<Clone>$") is null
            )
            .Select(type => type.FullName)
            .ToList();

        events.Should().HaveCountGreaterThan(20);
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void DomainEventListenersAlwaysAreSealedApplicationTypes()
    {
        var listeners = ProductionAssemblies
            .AllTypes.Where(type =>
                type is { IsInterface: false, IsAbstract: false }
                && type.GetInterfaces()
                    .Any(contract =>
                        contract.IsGenericType
                        && contract.GetGenericTypeDefinition() == typeof(IDomainEventListener<>)
                    )
            )
            .ToList();

        var offenders = listeners
            .Where(type => type.Assembly != ProductionAssemblies.Application || !type.IsSealed)
            .Select(type => type.FullName)
            .ToList();

        listeners.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }
}
