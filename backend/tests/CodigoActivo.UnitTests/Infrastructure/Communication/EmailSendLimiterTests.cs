using System.Globalization;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailSendLimiterTests
{
    private const string Recipient = "member@example.test";

    private static EmailGuardOptions Options()
    {
        return new EmailGuardOptions
        {
            RecipientBurst = 3,
            RecipientPerHour = 2,
            RecipientPerDay = 5,
            AnonymousBurst = 6,
            AnonymousPerHour = 6,
            MaxTrackedRecipients = 100,
        };
    }

    private static EmailSendLimiter Limiter(EmailGuardOptions options, TestClock clock)
    {
        return new EmailSendLimiter(options, new SmtpOptions(), clock);
    }

    private static EmailLimitScope Consume(EmailSendLimiter limiter, string address)
    {
        return limiter.TryConsume(EmailKind.AccessRecovery, address).Scope;
    }

    private static string Address(string prefix, int index)
    {
        return $"{prefix}{index.ToString(CultureInfo.InvariantCulture)}@example.test";
    }

    [Fact]
    public void TryConsumeBeyondTheRecipientBurstDeniesWithRecipientScope()
    {
        var limiter = Limiter(Options(), new TestClock());

        for (var i = 0; i < 3; i++)
        {
            Consume(limiter, Recipient).Should().Be(EmailLimitScope.None);
        }

        Consume(limiter, Recipient).Should().Be(EmailLimitScope.Recipient);
    }

    [Fact]
    public void TryConsumeAfterTheHourlyWindowRefillsAllowsAgain()
    {
        var clock = new TestClock();
        var limiter = Limiter(Options(), clock);

        for (var i = 0; i < 4; i++)
        {
            Consume(limiter, Recipient);
        }

        clock.UtcNow = clock.UtcNow.AddHours(1);

        Consume(limiter, Recipient).Should().Be(EmailLimitScope.None);
    }

    [Fact]
    public void TryConsumeDailyCapReachedDeniesEvenAfterTheHourlyWindowRefills()
    {
        var clock = new TestClock();
        var options = Options();
        options.AnonymousBurst = 1000;
        options.AnonymousPerHour = 1000;
        var limiter = Limiter(options, clock);

        var delivered = 0;
        for (var hour = 0; hour < 8; hour++)
        {
            while (Consume(limiter, Recipient) is EmailLimitScope.None)
            {
                delivered++;
            }

            clock.UtcNow = clock.UtcNow.AddHours(1);
        }

        delivered.Should().Be(6);
        Consume(limiter, Recipient).Should().Be(EmailLimitScope.Recipient);
    }

    [Fact]
    public void TryConsumeSubAddressedAndMixedCaseVariantsShareOneRecipientBudget()
    {
        var limiter = Limiter(Options(), new TestClock());

        Consume(limiter, "victim@example.test").Should().Be(EmailLimitScope.None);
        Consume(limiter, "  VICTIM@Example.TEST  ").Should().Be(EmailLimitScope.None);
        Consume(limiter, "victim+newsletter@example.test").Should().Be(EmailLimitScope.None);

        Consume(limiter, "victim+anything@example.test").Should().Be(EmailLimitScope.Recipient);
    }

    [Fact]
    public void NormalizeKeyGmailAddressFoldsDotsInTheLocalPart()
    {
        EmailSendLimiter
            .NormalizeKey("v.ic.tim+tag@GMAIL.com")
            .Should()
            .Be(EmailSendLimiter.NormalizeKey("victim@gmail.com"));

        EmailSendLimiter
            .NormalizeKey("v.ictim@googlemail.com")
            .Should()
            .Be("victim@googlemail.com");
    }

    [Fact]
    public void NormalizeKeyNonGmailDomainKeepsDotsSignificant()
    {
        EmailSendLimiter
            .NormalizeKey("first.last@example.test")
            .Should()
            .NotBe(EmailSendLimiter.NormalizeKey("firstlast@example.test"));
    }

    [Fact]
    public void TryConsumeAnonymousBudgetDrainedByOtherRecipientsDeniesWithGlobalScope()
    {
        var limiter = Limiter(Options(), new TestClock());

        for (var i = 0; i < 6; i++)
        {
            Consume(limiter, Address("member", i)).Should().Be(EmailLimitScope.None);
        }

        Consume(limiter, "fresh@example.test").Should().Be(EmailLimitScope.Global);
        limiter
            .TryConsume(EmailKind.AccountVerification, "newcomer@example.test")
            .Scope.Should()
            .Be(EmailLimitScope.Global);
    }

    [Fact]
    public void TryConsumeAnonymousFloodLeavesLoginCodesDeliverable()
    {
        var limiter = Limiter(Options(), new TestClock());

        for (var i = 0; i < 6; i++)
        {
            Consume(limiter, Address("spray", i));
        }

        for (var i = 0; i < 20; i++)
        {
            limiter
                .TryConsume(EmailKind.TwoFactorCode, Address("returning", i))
                .Scope.Should()
                .Be(EmailLimitScope.None, "login codes never spend the anonymous budget");
        }
    }

    [Fact]
    public void TryConsumeAnonymousBudgetNeverPassesHalfOfTheProviderPace()
    {
        var options = Options();
        options.AnonymousBurst = 1000;
        options.AnonymousPerHour = 1000;
        var limiter = new EmailSendLimiter(
            options,
            new SmtpOptions { MaxPerMinute = 1 },
            new TestClock()
        );

        var granted = Enumerable
            .Range(0, 100)
            .Count(i => Consume(limiter, Address("spray", i)) is EmailLimitScope.None);

        granted.Should().Be(30);
    }

    [Fact]
    public void TryConsumeResetMailFloodingAnAddressLeavesItsLoginCodesDeliverable()
    {
        var limiter = Limiter(Options(), new TestClock());

        for (var i = 0; i < 3; i++)
        {
            Consume(limiter, Recipient).Should().Be(EmailLimitScope.None);
        }

        Consume(limiter, Recipient).Should().Be(EmailLimitScope.Recipient);
        limiter
            .TryConsume(EmailKind.TwoFactorCode, Recipient)
            .Scope.Should()
            .Be(EmailLimitScope.None, "login codes have their own budget for each address");
    }

    [Fact]
    public void TryConsumeLoginCodesStayBoundedByTheirOwnRecipientBudget()
    {
        var limiter = Limiter(Options(), new TestClock());

        for (var i = 0; i < 3; i++)
        {
            limiter
                .TryConsume(EmailKind.TwoFactorCode, Recipient)
                .Scope.Should()
                .Be(EmailLimitScope.None);
        }

        limiter
            .TryConsume(EmailKind.TwoFactorCode, "MEMBER+tag@example.test")
            .Scope.Should()
            .Be(EmailLimitScope.Recipient);
        Consume(limiter, Recipient)
            .Should()
            .Be(EmailLimitScope.None, "other codes keep the budget of their own");
    }

    [Fact]
    public void TryConsumeRecipientDeniedLeavesTheAnonymousBudgetUntouched()
    {
        var limiter = Limiter(Options(), new TestClock());

        for (var i = 0; i < 3; i++)
        {
            Consume(limiter, Recipient);
        }

        for (var i = 0; i < 30; i++)
        {
            Consume(limiter, Recipient).Should().Be(EmailLimitScope.Recipient);
        }

        Consume(limiter, "bystander@example.test").Should().Be(EmailLimitScope.None);
    }

    [Fact]
    public void TryConsumeAnonymousDenialDoesNotStartTrackingTheRecipient()
    {
        var limiter = Limiter(Options(), new TestClock());

        for (var i = 0; i < 6; i++)
        {
            Consume(limiter, Address("member", i));
        }

        var tracked = limiter.TrackedRecipients;
        for (var i = 0; i < 50; i++)
        {
            Consume(limiter, Address("sprayed", i)).Should().Be(EmailLimitScope.Global);
        }

        limiter.TrackedRecipients.Should().Be(tracked);
    }

    [Fact]
    public void TryConsumeClockStepsBackwardsDoesNotMintTokens()
    {
        var clock = new TestClock();
        var limiter = Limiter(Options(), clock);

        for (var i = 0; i < 3; i++)
        {
            Consume(limiter, Recipient);
        }

        clock.UtcNow = clock.UtcNow.AddHours(-5);

        Consume(limiter, Recipient).Should().Be(EmailLimitScope.Recipient);
    }

    [Fact]
    public void TryConsumeTrackingTableSaturatedLetsTheAddressThroughAndAlerts()
    {
        var options = Options();
        options.MaxTrackedRecipients = 2;
        var limiter = Limiter(options, new TestClock());

        Consume(limiter, "first@example.test").Should().Be(EmailLimitScope.None);
        Consume(limiter, "second@example.test").Should().Be(EmailLimitScope.None);

        var decision = limiter.TryConsume(EmailKind.AccessRecovery, "third@example.test");

        decision.Scope.Should().Be(EmailLimitScope.None);
        decision.Alert.Should().Be(EmailGuardAlert.TrackingSaturated);
        limiter.TrackedRecipients.Should().Be(2);
    }

    [Fact]
    public void TryConsumeSweepRunsEvictsOnlyFullyRefilledRecipients()
    {
        var clock = new TestClock();
        var limiter = Limiter(Options(), clock);

        Consume(limiter, "barely-used@example.test");
        for (var i = 0; i < 3; i++)
        {
            Consume(limiter, Recipient);
        }

        limiter.TrackedRecipients.Should().Be(2);

        clock.UtcNow = clock.UtcNow.AddHours(6);
        Consume(limiter, "sweep-trigger@example.test");

        limiter.TrackedRecipients.Should().Be(2);
    }

    [Fact]
    public void TryConsumeFromManyThreadsGrantsExactlyTheBurst()
    {
        var options = Options();
        options.AnonymousBurst = 1000;
        options.AnonymousPerHour = 1000;
        var limiter = Limiter(options, new TestClock());

        var granted = 0;
        Parallel.For(
            0,
            200,
            _ =>
            {
                if (Consume(limiter, Recipient) is EmailLimitScope.None)
                {
                    Interlocked.Increment(ref granted);
                }
            }
        );

        granted.Should().Be(options.RecipientBurst);
    }
}
