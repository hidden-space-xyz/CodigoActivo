using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Services;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class EmailSenderWiringTests
{
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(IEmailSender).Assembly,
        typeof(EmailService).Assembly,
        typeof(SmtpEmailSender).Assembly,
        typeof(DependencyInjection).Assembly,
        typeof(ApiErrorResponseExtensions).Assembly,
    ];

    private static IEnumerable<Type> ProductionTypes()
    {
        return ProductionAssemblies.SelectMany(assembly => assembly.GetTypes());
    }

    [Fact]
    public void ProductionCode_OnlyTheThrottlingDecorator_ImplementsIEmailSender()
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
    public void ProductionCode_OnlyTheDecoratorAndTheAdminEmailService_DependOnIEmailTransport()
    {
        var consumers = ProductionTypes()
            .Where(type =>
                type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == typeof(IEmailTransport))
            )
            .ToList();

        consumers.Should().BeEquivalentTo([typeof(ThrottledEmailSender), typeof(EmailService)]);
    }

    [Fact]
    public void AddCodigoActivo_ResolvesTheGuardedSenderAndTheRawTransportSeparately()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["ACCOUNT_VERIFICATION_REQUIRED"] = "false" }
            )
            .Build();

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();

        provider.GetRequiredService<IEmailSender>().Should().BeOfType<ThrottledEmailSender>();
        provider.GetRequiredService<IEmailTransport>().Should().BeOfType<SmtpEmailSender>();
    }
}
