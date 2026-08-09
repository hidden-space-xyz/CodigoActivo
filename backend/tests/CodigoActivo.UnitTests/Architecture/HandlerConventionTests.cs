using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Repositories;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class HandlerConventionTests
{
    private static readonly Type[] WriteAndEmailPorts =
    [
        typeof(IUnitOfWork),
        typeof(ICacheInvalidator),
        typeof(IEmailSender),
        typeof(IEmailDispatcher),
        typeof(IEmailTransport),
    ];

    private static List<(Type Handler, Type Contract)> HandlerImplementations()
    {
        return
        [
            .. typeof(IQuery<>)
                .Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false })
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
}
