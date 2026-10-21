using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.API.Accounts;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Signups;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.Logs;
using CodigoActivo.Application.Reports;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Application.TermsDecisions.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Caching;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private static readonly Type[] ReadSidePorts =
    [
        typeof(IReadStore),
        typeof(IQueryExecutor),
        typeof(IDashboardCountsReader),
        typeof(ILogEntryReader),
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

    private static bool IsQueryHandler(Type type)
    {
        return type.GetInterfaces()
            .Prepend(type)
            .Any(candidate => IsHandlerContract(candidate) && !IsCommandContract(candidate));
    }

    private static IEnumerable<Type> ConstructorParameterTypes(Type type)
    {
        return type.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);
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
    public void HandlerUnitTestsAlwaysStartWithHandleAsync()
    {
        var testClassNames = HandlerImplementations()
            .Select(entry => entry.Handler.Name + "Tests")
            .ToHashSet(StringComparer.Ordinal);
        var testClasses = typeof(HandlerConventionTests)
            .Assembly.GetTypes()
            .Where(type => testClassNames.Contains(type.Name))
            .ToList();

        var offenders = testClasses
            .SelectMany(type =>
                type.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
            )
            .Where(method =>
                method.IsDefined(typeof(FactAttribute))
                && !method.Name.StartsWith("HandleAsync", StringComparison.Ordinal)
            )
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToList();

        testClasses.Should().NotBeEmpty();
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
                && ConstructorParameterTypes(entry.Handler).Any(WriteAndEmailPorts.Contains)
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
                ConstructorParameterTypes(entry.Handler)
                    .Where(dependency =>
                        dependency.Assembly == domain
                        || dependency.Name.EndsWith("Repository", StringComparison.Ordinal)
                    )
                    .Select(dependency => $"{entry.Handler.Name}({dependency.Name})")
            )
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void QueryHandlersAlwaysAreOnlyInjectedIntoOtherQueryHandlers()
    {
        var candidates = ProductionAssemblies
            .Application.GetTypes()
            .Where(type => !IsQueryHandler(type))
            .ToList();

        var offenders = candidates
            .SelectMany(type =>
                ConstructorParameterTypes(type)
                    .Where(IsQueryHandler)
                    .Select(dependency => $"{type.Name}({dependency.Name})")
            )
            .ToList();

        candidates.Should().Contain(type => type.GetInterfaces().Any(IsHandlerContract));
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void QueryHandlersOutsideApplicationAlwaysAreInjectedIntoTheReviewedTypes()
    {
        var consumers = ProductionAssemblies
            .Types(
                ProductionAssemblies.Infrastructure,
                ProductionAssemblies.Composition,
                ProductionAssemblies.Api
            )
            .Where(type => ConstructorParameterTypes(type).Any(IsQueryHandler))
            .ToList();

        consumers
            .Should()
            .BeEquivalentTo([
                typeof(CachingQueryDecorator<,>),
                typeof(SessionTicketValidator),
                typeof(TwoFactorTicketValidator),
            ]);
    }

    [Fact]
    public void ControllerServicesAlwaysAreHandlersOrTheReviewedOnes()
    {
        var controllers = ProductionAssemblies
            .Api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .ToList();
        var actionServices = controllers
            .SelectMany(type =>
                type.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
            )
            .SelectMany(action => action.GetParameters())
            .Where(parameter =>
                parameter.IsDefined(typeof(FromServicesAttribute), inherit: true)
                || parameter.IsDefined(typeof(FromKeyedServicesAttribute), inherit: true)
            )
            .Select(parameter => parameter.ParameterType);

        var services = controllers
            .SelectMany(ConstructorParameterTypes)
            .Concat(actionServices)
            .ToList();
        var reviewed = services.Where(service => !IsHandlerContract(service)).Distinct().ToList();

        services.Should().Contain(service => IsHandlerContract(service));
        reviewed
            .Should()
            .BeEquivalentTo([
                typeof(IAntiforgery),
                typeof(ILogger<AuthController>),
                typeof(SessionTicketValidator),
                typeof(TwoFactorTicketValidator),
                typeof(ApplicationOptions),
            ]);
    }

    [Fact]
    public void ReadSideConsumersOutsideQueryHandlersAlwaysAreTheReviewedOnes()
    {
        var consumers = ProductionAssemblies
            .Application.GetTypes()
            .Where(type =>
                !IsQueryHandler(type) && ConstructorParameterTypes(type).Any(ReadSidePorts.Contains)
            )
            .ToList();

        consumers
            .Should()
            .BeEquivalentTo([
                typeof(SendEmailToEventAttendeesCommandHandler),
                typeof(SendEmailToUserCommandHandler),
                typeof(SendEmailToUsersCommandHandler),
                typeof(ActingUserPolicy),
                typeof(ActivitySignupNotifier),
                typeof(ConsentWithdrawalNotification),
            ]);
    }

    [Fact]
    public void QueriesNamingAPersonAlwaysAreTheReviewedOnes()
    {
        var namingAPerson = HandlerImplementations()
            .Where(entry => !IsCommandContract(entry.Contract))
            .Select(entry => MessageOf(entry.Contract))
            .Where(query =>
                query
                    .GetProperties()
                    .Any(property =>
                        property.Name == "IsAdmin" || MentionsUserId(property.PropertyType)
                    )
            )
            .Select(query => query.Name)
            .ToList();

        namingAPerson
            .Should()
            .BeEquivalentTo(
                "GetAccountQuery",
                "GetAssignmentQuery",
                "GetAssignmentsQuery",
                "GetEventTermsSignatureQuery",
                "GetSessionIdentityQuery",
                "GetUserByIdQuery",
                "VerifyTimeOverlapsQuery"
            );
    }

    private static bool MentionsUserId(Type type)
    {
        return type == typeof(UserId)
            || (type.IsGenericType && type.GetGenericArguments().Any(MentionsUserId));
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
