using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class HandlerRegistrationTests
{
    private static bool IsUseCaseContract(Type type)
    {
        return type.IsGenericType
            && (
                type.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                || type.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)
                || type.GetGenericTypeDefinition() == typeof(IDomainEventListener<>)
            );
    }

    private static List<(Type Contract, Type Implementation)> DiscoveredHandlers()
    {
        return
        [
            .. typeof(IQuery<>)
                .Assembly.GetTypes()
                .Where(type =>
                    type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                )
                .SelectMany(type =>
                    type.GetInterfaces()
                        .Where(IsUseCaseContract)
                        .Select(contract => (contract, type))
                ),
        ];
    }

    private static ServiceCollection BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["SMTP_HOST"] = "smtp.example.test",
                    ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
                    ["FileStorage:RootPath"] = Path.GetTempPath(),
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<ICacheInvalidator>());
        services.AddSingleton(Substitute.For<ICurrentUser>());
        services.AddCodigoActivo(configuration);
        return services;
    }

    [Fact]
    public void AddCodigoActivoRegistersEveryDiscoveredHandlerByItsContract()
    {
        var services = BuildServices();

        services
            .Where(descriptor => IsUseCaseContract(descriptor.ServiceType))
            .Select(descriptor => descriptor.ServiceType)
            .Should()
            .BeEquivalentTo(DiscoveredHandlers().Select(handler => handler.Contract));
        services
            .Where(descriptor => IsUseCaseContract(descriptor.ServiceType))
            .Should()
            .OnlyContain(descriptor => descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddCodigoActivoEveryHandlerResolvesInsideAScope()
    {
        using var provider = BuildServices().BuildServiceProvider();
        using var scope = provider.CreateScope();

        foreach (var (contract, _) in DiscoveredHandlers())
        {
            scope.ServiceProvider.GetRequiredService(contract).Should().NotBeNull();
        }
    }

    [Fact]
    public void AddCodigoActivoCommandsRunThroughLoggingValidationAndTheUnitOfWork()
    {
        using var provider = BuildServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var command = DiscoveredHandlers()
            .First(handler =>
                handler.Contract.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
            )
            .Contract;

        var resolved = scope.ServiceProvider.GetRequiredService(command);

        resolved
            .GetType()
            .GetGenericTypeDefinition()
            .Should()
            .Be(typeof(LoggingCommandDecorator<,>));
    }

    [Fact]
    public void AddCodigoActivoQueriesRunThroughLoggingValidationAndCaching()
    {
        using var provider = BuildServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var query = DiscoveredHandlers()
            .First(handler =>
                handler.Contract.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)
            )
            .Contract;

        var resolved = scope.ServiceProvider.GetRequiredService(query);

        resolved.GetType().GetGenericTypeDefinition().Should().Be(typeof(LoggingQueryDecorator<,>));
    }
}
