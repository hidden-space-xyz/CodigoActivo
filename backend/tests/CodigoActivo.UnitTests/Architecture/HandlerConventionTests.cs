using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class HandlerConventionTests
{
    private static readonly Type[] WriteAndEmailPorts =
    [
        typeof(IUnitOfWork),
        typeof(ICacheInvalidator),
        typeof(IEmailSender),
        typeof(IEmailOutbox),
        typeof(IEmailTransport),
    ];

    private static List<(Type Handler, Type Contract)> HandlerImplementations()
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
                        .Where(IsHandlerContract)
                        .Select(contract => (Handler: type, Contract: contract))
                ),
        ];
    }

    private static bool IsHandlerContract(Type candidate)
    {
        return candidate.IsGenericType
            && (
                candidate.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                || candidate.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)
            );
    }

    private static bool IsCommandContract(Type contract)
    {
        return contract.GetGenericTypeDefinition() == typeof(ICommandHandler<,>);
    }

    private static Type MessageOf(Type contract)
    {
        return contract.GetGenericArguments()[0];
    }

    [Fact]
    public void HandlersAlwaysAreSealed()
    {
        var offenders = HandlerImplementations()
            .Select(entry => entry.Handler)
            .Distinct()
            .Where(handler => !handler.IsSealed)
            .Select(handler => handler.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void HandlersAlwaysImplementExactlyOneHandlerContract()
    {
        var offenders = HandlerImplementations()
            .GroupBy(entry => entry.Handler)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void HandlersAlwaysAreNamedAfterTheirMessage()
    {
        var offenders = HandlerImplementations()
            .Where(entry =>
                !string.Equals(
                    entry.Handler.Name,
                    MessageOf(entry.Contract).Name + "Handler",
                    StringComparison.Ordinal
                )
            )
            .Select(entry => entry.Handler.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void MessagesAlwaysCarryTheSuffixOfTheirContract()
    {
        var offenders = HandlerImplementations()
            .Where(entry =>
                !MessageOf(entry.Contract)
                    .Name.EndsWith(
                        IsCommandContract(entry.Contract) ? "Command" : "Query",
                        StringComparison.Ordinal
                    )
            )
            .Select(entry => MessageOf(entry.Contract).Name)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void MessagesAndHandlersAlwaysShareAnAggregateCommandsOrQueriesNamespace()
    {
        var offenders = HandlerImplementations()
            .Where(entry =>
            {
                var segment = IsCommandContract(entry.Contract) ? ".Commands" : ".Queries";
                return entry.Handler.Namespace is not { } ns
                    || !string.Equals(
                        ns,
                        MessageOf(entry.Contract).Namespace,
                        StringComparison.Ordinal
                    )
                    || !ns.StartsWith("CodigoActivo.Application.", StringComparison.Ordinal)
                    || !ns.EndsWith(segment, StringComparison.Ordinal);
            })
            .Select(entry => entry.Handler.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void MessagesAlwaysHaveExactlyOneHandler()
    {
        var messages = typeof(IQuery<>)
            .Assembly.GetTypes()
            .Where(type =>
                type is { IsAbstract: false }
                && type.GetInterfaces()
                    .Any(candidate =>
                        candidate.IsGenericType
                        && (
                            candidate.GetGenericTypeDefinition() == typeof(ICommand<>)
                            || candidate.GetGenericTypeDefinition() == typeof(IQuery<>)
                        )
                    )
            )
            .ToList();

        var handled = HandlerImplementations().Select(entry => MessageOf(entry.Contract)).ToList();

        handled.Should().OnlyHaveUniqueItems();
        messages.Should().BeEquivalentTo(handled);
    }

    [Fact]
    public void QueryHandlersConstructorsNeverDependOnWriteOrEmailPorts()
    {
        var offenders = HandlerImplementations()
            .Where(entry =>
                !IsCommandContract(entry.Contract)
                && entry
                    .Handler.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => WriteAndEmailPorts.Contains(parameter.ParameterType))
            )
            .Select(entry => entry.Handler.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void QueryHandlersConstructorsNeverDependOnRepositoriesOrDomainTypes()
    {
        var domain = typeof(Result).Assembly;

        var offenders = HandlerImplementations()
            .Where(entry => !IsCommandContract(entry.Contract))
            .SelectMany(entry =>
                entry
                    .Handler.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Where(parameter =>
                        parameter.ParameterType.Assembly == domain
                        || parameter.ParameterType.Name.EndsWith(
                            "Repository",
                            StringComparison.Ordinal
                        )
                    )
                    .Select(parameter => $"{entry.Handler.Name}({parameter.ParameterType.Name})")
            )
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void QueryHandlersAlwaysAreOnlyInjectedIntoOtherQueryHandlers()
    {
        var queryHandlers = HandlerImplementations()
            .Where(entry => !IsCommandContract(entry.Contract))
            .Select(entry => entry.Handler)
            .ToHashSet();

        var offenders = typeof(IQuery<>)
            .Assembly.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } && !queryHandlers.Contains(type)
            )
            .Where(type =>
                type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => queryHandlers.Contains(parameter.ParameterType))
            )
            .Select(type => type.Name)
            .ToList();

        queryHandlers.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void CommandResultsNeverCarryResponseContracts()
    {
        var offenders = HandlerImplementations()
            .Where(entry => IsCommandContract(entry.Contract))
            .Where(entry => CarriesResponseContract(entry.Contract.GetGenericArguments()[1]))
            .Select(entry => entry.Handler.Name)
            .ToList();

        offenders.Should().BeEmpty();
    }

    private static bool CarriesResponseContract(Type type)
    {
        if (type.Name.EndsWith("Response", StringComparison.Ordinal))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(CarriesResponseContract);
    }
}
