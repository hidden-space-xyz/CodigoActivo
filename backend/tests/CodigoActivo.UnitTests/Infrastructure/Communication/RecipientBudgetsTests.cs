using AwesomeAssertions;
using CodigoActivo.Infrastructure.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class RecipientBudgetsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RefundSpentMessageLetsTheAddressSendItAgain()
    {
        var budgets = new RecipientBudgets(new EmailGuardOptions { RecipientBurst = 1 }, Now);
        budgets.TrySpend("ana@example.test", Now).Allowed.Should().BeTrue();
        budgets.TrySpend("ana@example.test", Now).Allowed.Should().BeFalse();

        budgets.Refund("ana@example.test", Now);

        budgets.TrySpend("ana@example.test", Now).Allowed.Should().BeTrue();
    }

    [Fact]
    public void RefundUntrackedAddressStartsNoBudget()
    {
        var budgets = new RecipientBudgets(new EmailGuardOptions(), Now);

        budgets.Refund("ana@example.test", Now);

        budgets.Count.Should().Be(0);
    }
}
