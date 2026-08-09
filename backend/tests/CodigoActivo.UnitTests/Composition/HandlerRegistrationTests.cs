using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class HandlerRegistrationTests
{
    private static bool IsHandlerType(Type type)
    {
        return type is { IsClass: true, IsAbstract: false }
            && type.GetInterfaces()
                .Any(candidate =>
                    candidate.IsGenericType
                    && (
                        candidate.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                        || candidate.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)
                    )
                );
    }

    private static List<Type> HandlerTypes()
    {
        return [.. typeof(IQuery<>).Assembly.GetTypes().Where(IsHandlerType)];
    }

    private static ServiceCollection BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ACCOUNT_VERIFICATION_REQUIRED"] = "false",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<ICacheInvalidator>());
        services.AddCodigoActivo(configuration);
        return services;
    }

    [Fact]
    public void AddCodigoActivoHandlerDescriptorsMatchTheDiscoveredHandlersExactly()
    {
        var services = BuildServices();

        var registered = services
            .Where(descriptor => IsHandlerType(descriptor.ServiceType))
            .ToList();

        registered
            .Select(descriptor => descriptor.ServiceType)
            .Should()
            .BeEquivalentTo(HandlerTypes());
        registered
            .Should()
            .OnlyContain(descriptor =>
                descriptor.Lifetime == ServiceLifetime.Scoped
                && descriptor.ImplementationType == descriptor.ServiceType
            );
    }

    [Fact]
    public void AddCodigoActivoEveryHandlerResolvesInsideAScope()
    {
        using var provider = BuildServices().BuildServiceProvider();
        using var scope = provider.CreateScope();

        foreach (var handler in HandlerTypes())
        {
            scope.ServiceProvider.GetRequiredService(handler).Should().NotBeNull();
        }
    }
}
