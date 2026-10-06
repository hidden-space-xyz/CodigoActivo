using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Infrastructure.Caching;
using CodigoActivo.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodigoActivo.Composition;

/// <summary>
/// Registers every command handler, query handler and domain event listener of the application
/// by the contract it implements, and wraps the handlers in the cross-cutting decorators.
/// </summary>
public static class UseCaseRegistration
{
    private static readonly Type[] Contracts =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(IDomainEventListener<>),
    ];

    /// <summary>
    /// Adds the use cases. From the outside in, a command runs through logging, validation and
    /// the unit of work before its handler; a query runs through logging, validation and caching.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        foreach (var (contract, implementation) in Implementations())
        {
            services.AddScoped(contract, implementation);
        }

        foreach (
            var listener in typeof(ICommittedEventsHandler)
                .Assembly.GetTypes()
                .Where(type =>
                    type is { IsClass: true, IsAbstract: false }
                    && typeof(ICommittedEventsHandler).IsAssignableFrom(type)
                )
        )
        {
            services.TryAddEnumerable(
                ServiceDescriptor.Scoped(typeof(ICommittedEventsHandler), listener)
            );
        }

        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.AddScoped<MessageValidator>();

        services.Decorate(typeof(ICommandHandler<,>), typeof(UnitOfWorkCommandDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationCommandDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(LoggingCommandDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(CachingQueryDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(ValidationQueryDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(LoggingQueryDecorator<,>));
        return services;
    }

    /// <summary>
    /// Lists every concrete handler and listener of the application with the contract it implements.
    /// </summary>
    /// <returns>Pairs of closed contract and implementation.</returns>
    public static IReadOnlyList<(Type Contract, Type Implementation)> Implementations()
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
                        .Where(contract =>
                            contract.IsGenericType
                            && Contracts.Contains(contract.GetGenericTypeDefinition())
                        )
                        .Select(contract => (contract, type))
                ),
        ];
    }

    private static void Decorate(
        this IServiceCollection services,
        Type openContract,
        Type openDecorator
    )
    {
        var decorated = services
            .Select((descriptor, index) => (descriptor, index))
            .Where(entry =>
                entry.descriptor.ServiceType.IsGenericType
                && entry.descriptor.ServiceType.GetGenericTypeDefinition() == openContract
            )
            .ToList();

        foreach (var (descriptor, index) in decorated)
        {
            var decorator = openDecorator.MakeGenericType(
                descriptor.ServiceType.GetGenericArguments()
            );
            services[index] = ServiceDescriptor.Describe(
                descriptor.ServiceType,
                provider =>
                    ActivatorUtilities.CreateInstance(
                        provider,
                        decorator,
                        CreateInner(provider, descriptor)
                    ),
                descriptor.Lifetime
            );
        }
    }

    private static object CreateInner(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        return descriptor switch
        {
            { ImplementationFactory: { } factory } => factory(provider),
            { ImplementationType: { } type } => ActivatorUtilities.CreateInstance(provider, type),
            { ImplementationInstance: { } instance } => instance,
            _ => throw new InvalidOperationException(
                $"{descriptor.ServiceType.Name} cannot be decorated."
            ),
        };
    }
}
