using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Emails;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class EmailSenderWiringTests
{
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(IEmailSender).Assembly,
        typeof(ManualEmailDispatcher).Assembly,
        typeof(SmtpEmailSender).Assembly,
        typeof(DependencyInjection).Assembly,
        typeof(ApiErrorResponseExtensions).Assembly,
    ];

    private static IEnumerable<Type> ProductionTypes()
    {
        return ProductionAssemblies.SelectMany(assembly => assembly.GetTypes());
    }

    [Fact]
    public void ProductionCodeOnlyTheThrottlingDecoratorImplementsIEmailSender()
    {
        var implementations = ProductionTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && typeof(IEmailSender).IsAssignableFrom(type)
            )
            .ToList();

        implementations.Should().BeEquivalentTo([typeof(ThrottledEmailSender)]);
    }

    [Fact]
    public void ProductionCodeOnlyTheQueueDrainAndTheManualEmailDispatcherDependOnIEmailTransport()
    {
        var consumers = ProductionTypes()
            .Where(type =>
                type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == typeof(IEmailTransport))
            )
            .ToList();

        consumers
            .Should()
            .BeEquivalentTo([typeof(ChannelEmailDispatcher), typeof(ManualEmailDispatcher)]);
    }

    [Fact]
    public void ProductionCodeOnlyTheThrottlingDecoratorDependsOnIEmailDispatcher()
    {
        var consumers = ProductionTypes()
            .Where(type =>
                type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == typeof(IEmailDispatcher))
            )
            .ToList();

        consumers.Should().BeEquivalentTo([typeof(ThrottledEmailSender)]);
    }

    [Fact]
    public void AddCodigoActivoResolvesTheGuardedSenderAndTheRawTransportSeparately()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ACCOUNT_VERIFICATION_REQUIRED"] = "false",
                }
            )
            .Build();

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();

        provider.GetRequiredService<IEmailSender>().Should().BeOfType<ThrottledEmailSender>();
        provider.GetRequiredService<IEmailTransport>().Should().BeOfType<SmtpEmailSender>();
        provider
            .GetRequiredService<IEmailDispatcher>()
            .Should()
            .BeSameAs(provider.GetRequiredService<ChannelEmailDispatcher>());
        provider
            .GetServices<IHostedService>()
            .Should()
            .Contain(provider.GetRequiredService<ChannelEmailDispatcher>());
    }
}
