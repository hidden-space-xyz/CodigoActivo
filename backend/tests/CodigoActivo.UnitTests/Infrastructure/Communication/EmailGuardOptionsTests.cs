using AwesomeAssertions;
using CodigoActivo.Infrastructure.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailGuardOptionsTests
{
    [Fact]
    public void AnonymousBudgetSlowProviderKeepsItAtHalfOfWhatTheProviderDelivers()
    {
        var options = new EmailGuardOptions();

        options.AnonymousBudget(providerPerMinute: 10).Should().Be((300, 300));
    }

    [Fact]
    public void AnonymousBudgetFastProviderKeepsTheConfiguredBudget()
    {
        var options = new EmailGuardOptions();

        options.AnonymousBudget(providerPerMinute: 600).Should().Be((1000, 1000));
    }
}
