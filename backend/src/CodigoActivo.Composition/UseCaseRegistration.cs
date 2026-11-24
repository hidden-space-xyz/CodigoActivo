using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Infrastructure.Caching;
using CodigoActivo.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition;

/// <summary>
/// Registers every command handler, query handler, domain event listener and committed events
/// handler of the application by the contract it implements, and wraps the handlers in the
/// cross-cutting decorators.
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
    /// Adds the use cases. From the outside in, a command runs through logging, the audit log,
    /// validation and the unit of work before its handler; a query runs through logging, the audit
    /// log, validation and caching.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.Scan(scan =>
            scan.FromAssemblyOf<ICommittedEventsHandler>()
                .AddClasses(
                    classes =>
                        classes
                            .AssignableToAny(Contracts)
                            .Where(type => !type.IsGenericTypeDefinition),
                    publicOnly: false
                )
                .As(type => type.GetInterfaces().Where(IsContract))
                .WithScopedLifetime()
                .AddClasses(
                    classes => classes.AssignableTo<ICommittedEventsHandler>(),
                    publicOnly: false
                )
                .As<ICommittedEventsHandler>()
                .WithScopedLifetime()
        );

        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.AddScoped<MessageValidator>();
        services.AddScoped<IAuditSubject, AuditSubject>();
        services.AddScoped<AuditWriter>();
        services.AddScoped<ICommandScope, CommandScope>();
        services.AddHostedService<ScheduledJobsWorker>();

        services.Decorate(typeof(ICommandHandler<,>), typeof(UnitOfWorkCommandDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationCommandDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(AuditCommandDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(LoggingCommandDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(CachingQueryDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(ValidationQueryDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(AuditQueryDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(LoggingQueryDecorator<,>));
        return services;
    }

    private static bool IsContract(Type contract)
    {
        return contract.IsGenericType && Contracts.Contains(contract.GetGenericTypeDefinition());
    }
}
