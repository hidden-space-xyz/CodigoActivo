using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Emails;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class EmailSenderWiringTests
{
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(CodigoActivo.Domain.Common.Result).Assembly,
        typeof(IEmailSender).Assembly,
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
            .ContainSingle(service => service is EmailOutboxDeliveryWorker);
    }

    [Theory]
    [InlineData(null, null, SmtpSecurityMode.StartTls)]
    [InlineData("465", null, SmtpSecurityMode.SslOnConnect)]
    [InlineData("2525", "", SmtpSecurityMode.StartTls)]
    [InlineData("465", "StartTls", SmtpSecurityMode.StartTls)]
    [InlineData("587", "SslOnConnect", SmtpSecurityMode.SslOnConnect)]
    public void AddCodigoActivoSmtpSecurityDefaultsToTheEncryptionOfThePort(
        string? port,
        string? security,
        SmtpSecurityMode expected
    )
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SMTP_PORT"] = port,
                ["SMTP_SECURITY"] = security,
            }
        );

        provider.GetRequiredService<SmtpOptions>().Security.Should().Be(expected);
    }

    [Fact]
    public void AddCodigoActivoSmtpSenderNameDefaultsToTheAssociation()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<SmtpOptions>().FromName.Should().Be(Organization.Name);
    }

    [Theory]
    [InlineData(null, "no-reply@example.test")]
    [InlineData("smtp.example.test", null)]
    [InlineData(null, null)]
    public void AddCodigoActivoSmtpUnconfiguredStopsTheStart(string? host, string? from)
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["SMTP_HOST"] = host,
                ["SMTP_FROM_ADDRESS"] = from,
            }
        );

        var act = () => provider.GetRequiredService<SmtpOptions>();

        act.Should()
            .Throw<OptionsValidationException>()
            .WithMessage("*SMTP is not configured*Login codes are delivered by email*");
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
