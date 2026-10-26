using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Emails;
using CodigoActivo.Infrastructure.Communication;
using Xunit;

namespace CodigoActivo.ArchitectureTests;

public sealed class EmailConventionTests
{
    private static List<Type> ConsumersOf(Type port)
    {
        return
        [
            .. ProductionAssemblies.AllTypes.Where(type =>
                type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == port)
            ),
        ];
    }

    [Fact]
    public void EmailSenderIsOnlyImplementedByTheThrottlingDecorator()
    {
        var implementations = ProductionAssemblies
            .AllTypes.Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && typeof(IEmailSender).IsAssignableFrom(type)
            )
            .ToList();

        implementations.Should().BeEquivalentTo([typeof(ThrottledEmailSender)]);
    }

    [Fact]
    public void EmailTransportIsOnlyInjectedIntoTheOutboxDeliverer()
    {
        ConsumersOf(typeof(IEmailTransport))
            .Should()
            .BeEquivalentTo([typeof(EmailOutboxDeliverer)]);
    }

    [Fact]
    public void EmailOutboxIsOnlyInjectedIntoTheThrottlingDecoratorAndTheManualDispatcher()
    {
        ConsumersOf(typeof(IEmailOutbox))
            .Should()
            .BeEquivalentTo([typeof(ThrottledEmailSender), typeof(ManualEmailDispatcher)]);
    }
}
