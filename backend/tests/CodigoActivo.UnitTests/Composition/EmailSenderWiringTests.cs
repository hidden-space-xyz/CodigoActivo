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
    public void ProductionCodeOnlyTheOutboxDelivererDependsOnIEmailTransport()
    {
        var consumers = ProductionTypes()
            .Where(type =>
                type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == typeof(IEmailTransport))
            )
            .ToList();

        consumers.Should().BeEquivalentTo([typeof(EmailOutboxDeliverer)]);
    }

    [Fact]
    public void ProductionCodeOnlyTheThrottlingDecoratorAndTheManualDispatcherDependOnIEmailOutbox()
    {
        var consumers = ProductionTypes()
            .Where(type =>
                type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == typeof(IEmailOutbox))
            )
            .ToList();

        consumers
            .Should()
            .BeEquivalentTo([typeof(ThrottledEmailSender), typeof(ManualEmailDispatcher)]);
    }

    [Fact]
    public void AddCodigoActivoResolvesTheGuardedSenderAndTheRawTransportSeparately()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<IEmailSender>().Should().BeOfType<ThrottledEmailSender>();
        provider.GetRequiredService<IEmailTransport>().Should().BeOfType<SmtpEmailSender>();
    }

    [Fact]
    public void AddCodigoActivoResolvesOneOutboxSharedByTheSenderAndTheDeliveryWorker()
    {
        using var provider = BuildProvider();

        provider
            .GetRequiredService<IEmailOutbox>()
            .Should()
            .BeSameAs(provider.GetRequiredService<IEmailOutboxStore>())
            .And.BeOfType<EmailOutboxStore>();
        provider.GetRequiredService<EmailOutboxProtector>().Should().NotBeNull();
        provider
            .GetRequiredService<EmailOutboxSignal>()
            .Should()
            .BeSameAs(provider.GetRequiredService<EmailOutboxSignal>());
        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service is EmailOutboxProcessor);
    }

    [Fact]
    public void AddCodigoActivoAppliesTheDefaultQueuePolicy()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<EmailQueueOptions>();

        options.Capacity.Should().Be(EmailQueueOptions.DefaultCapacity);
        options.Workers.Should().Be(EmailQueueOptions.DefaultWorkers);
        options.BatchSize.Should().Be(EmailQueueOptions.DefaultBatchSize);
        options.PollInterval.Should().Be(EmailQueueOptions.DefaultPollInterval);
        options.SendTimeout.Should().Be(EmailQueueOptions.DefaultSendTimeout);
        options.ShutdownDrain.Should().Be(EmailQueueOptions.DefaultShutdownDrain);
    }

    [Fact]
    public void AddCodigoActivoQueueSettingsAreReadAndClamped()
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["EmailQueue:Capacity"] = "25",
                ["EmailQueue:Workers"] = "999",
                ["EmailQueue:BatchSize"] = "9999",
                ["EmailQueue:PollIntervalSeconds"] = "2",
                ["EmailQueue:SendTimeoutSeconds"] = "99999",
                ["EmailQueue:ShutdownDrainSeconds"] = "0",
            }
        );

        var options = provider.GetRequiredService<EmailQueueOptions>();

        options.Capacity.Should().Be(25);
        options.Workers.Should().Be(EmailQueueOptions.MaxWorkers);
        options.BatchSize.Should().Be(EmailQueueOptions.MaxBatchSize);
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(2));
        options.SendTimeout.Should().Be(EmailQueueOptions.MaxSendTimeout);
        options.ShutdownDrain.Should().Be(EmailQueueOptions.DefaultShutdownDrain);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SMTP_HOST"] = "smtp.example.test",
            ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
        };
        foreach (var (key, value) in settings ?? [])
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
    }
}
