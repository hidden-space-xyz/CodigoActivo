using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
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
            GlobalBurst = 10,
            GlobalPerHour = 10,
            GlobalCredentialReserve = 4,
            MaxTrackedRecipients = 100,
        };
    }

    private static EmailLimitScope Consume(EmailSendLimiter limiter, string address)
    {
        return limiter.TryConsume(EmailKind.ActivityNotification, address).Scope;
    }

    [Fact]
    public void TryConsume_BeyondTheRecipientBurst_DeniesWithRecipientScope()
    {
        var limiter = new EmailSendLimiter(Options(), new TestClock());

        for (var i = 0; i < 3; i++)
            Consume(limiter, Recipient).Should().Be(EmailLimitScope.None);

        Consume(limiter, Recipient).Should().Be(EmailLimitScope.Recipient);
    }

    [Fact]
    public void TryConsume_AfterTheHourlyWindowRefills_AllowsAgain()
    {
        var clock = new TestClock();
        var limiter = new EmailSendLimiter(Options(), clock);

        for (var i = 0; i < 4; i++)
            Consume(limiter, Recipient);

        clock.UtcNow = clock.UtcNow.AddHours(1);

        Consume(limiter, Recipient).Should().Be(EmailLimitScope.None);
    }

    [Fact]
    public void TryConsume_DailyCapReached_DeniesEvenAfterTheHourlyWindowRefills()
    {
        var clock = new TestClock();
        var limiter = new EmailSendLimiter(Options(), clock);

        var delivered = 0;
        for (var hour = 0; hour < 8; hour++)
        {
            while (Consume(limiter, Recipient) == EmailLimitScope.None)
                delivered++;

            clock.UtcNow = clock.UtcNow.AddHours(1);
        }

        delivered.Should().Be(6);
        Consume(limiter, Recipient).Should().Be(EmailLimitScope.Recipient);
    }

    [Fact]
    public void TryConsume_SubAddressedAndMixedCaseVariants_ShareOneRecipientBudget()
    {
        var limiter = new EmailSendLimiter(Options(), new TestClock());

        Consume(limiter, "victim@example.test").Should().Be(EmailLimitScope.None);
        Consume(limiter, "  VICTIM@Example.TEST  ").Should().Be(EmailLimitScope.None);
        Consume(limiter, "victim+newsletter@example.test").Should().Be(EmailLimitScope.None);

        Consume(limiter, "victim+anything@example.test").Should().Be(EmailLimitScope.Recipient);
    }

    [Fact]
    public void NormalizeKey_GmailAddress_FoldsDotsInTheLocalPart()
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
    public void NormalizeKey_NonGmailDomain_KeepsDotsSignificant()
    {
        EmailSendLimiter
            .NormalizeKey("first.last@example.test")
            .Should()
            .NotBe(EmailSendLimiter.NormalizeKey("firstlast@example.test"));
    }

    [Fact]
    public void TryConsume_GlobalBudgetDrainedByOtherRecipients_DeniesWithGlobalScope()
    {
        var limiter = new EmailSendLimiter(Options(), new TestClock());

        for (var i = 0; i < 6; i++)
            Consume(limiter, $"member{i}@example.test").Should().Be(EmailLimitScope.None);

        Consume(limiter, "fresh@example.test").Should().Be(EmailLimitScope.Global);
    }

    [Fact]
    public void TryConsume_ActivityMailExhaustedTheBudget_StillDeliversAccountEmail()
    {
        var limiter = new EmailSendLimiter(Options(), new TestClock());

        for (var i = 0; i < 6; i++)
            Consume(limiter, $"member{i}@example.test");

        limiter
            .TryConsume(EmailKind.PasswordReset, "locked-out@example.test")
            .Scope.Should()
            .Be(EmailLimitScope.None);
        limiter
            .TryConsume(EmailKind.AccountVerification, "newcomer@example.test")
            .Scope.Should()
            .Be(EmailLimitScope.None);
    }

    [Fact]
    public void TryConsume_ReserveWiderThanTheGlobalBurst_StillDeliversAutomaticMail()
    {
        var limiter = new EmailSendLimiter(
            new EmailGuardOptions { GlobalBurst = 6 },
            new TestClock()
        );

        Consume(limiter, Recipient).Should().Be(EmailLimitScope.None);
    }

    [Fact]
    public void TryConsume_RecipientDenied_LeavesTheGlobalBudgetUntouched()
    {
        var limiter = new EmailSendLimiter(Options(), new TestClock());

        for (var i = 0; i < 3; i++)
            Consume(limiter, Recipient);

        for (var i = 0; i < 30; i++)
            Consume(limiter, Recipient).Should().Be(EmailLimitScope.Recipient);

        Consume(limiter, "bystander@example.test").Should().Be(EmailLimitScope.None);
    }

    [Fact]
    public void TryConsume_GlobalDenial_DoesNotStartTrackingTheRecipient()
    {
        var limiter = new EmailSendLimiter(Options(), new TestClock());

        for (var i = 0; i < 6; i++)
            Consume(limiter, $"member{i}@example.test");

        var tracked = limiter.TrackedRecipients;
        for (var i = 0; i < 50; i++)
            Consume(limiter, $"sprayed{i}@example.test").Should().Be(EmailLimitScope.Global);

        limiter.TrackedRecipients.Should().Be(tracked);
    }

    [Fact]
    public void TryConsume_ClockStepsBackwards_DoesNotMintTokens()
    {
        var clock = new TestClock();
        var limiter = new EmailSendLimiter(Options(), clock);

        for (var i = 0; i < 3; i++)
            Consume(limiter, Recipient);

        clock.UtcNow = clock.UtcNow.AddHours(-5);

        Consume(limiter, Recipient).Should().Be(EmailLimitScope.Recipient);
    }

    [Fact]
    public void TryConsume_TrackingTableSaturated_FallsBackToTheGlobalBudget()
    {
        var options = Options();
        options.MaxTrackedRecipients = 2;
        var limiter = new EmailSendLimiter(options, new TestClock());

        Consume(limiter, "first@example.test").Should().Be(EmailLimitScope.None);
        Consume(limiter, "second@example.test").Should().Be(EmailLimitScope.None);

        var decision = limiter.TryConsume(EmailKind.ActivityNotification, "third@example.test");

        decision.Scope.Should().Be(EmailLimitScope.None);
        decision.Alert.Should().Be(EmailGuardAlert.TrackingSaturated);
        limiter.TrackedRecipients.Should().Be(2);
    }

    [Fact]
    public void TryConsume_SweepRuns_EvictsOnlyFullyRefilledRecipients()
    {
        var clock = new TestClock();
        var limiter = new EmailSendLimiter(Options(), clock);

        Consume(limiter, "barely-used@example.test");
        for (var i = 0; i < 3; i++)
            Consume(limiter, Recipient);

        limiter.TrackedRecipients.Should().Be(2);

        clock.UtcNow = clock.UtcNow.AddHours(6);
        Consume(limiter, "sweep-trigger@example.test");

        limiter.TrackedRecipients.Should().Be(2);
    }

    [Fact]
    public void TryConsume_ShippedDefaults_LetAnEventOpeningAndItsRosterReviewThrough()
    {
        var limiter = new EmailSendLimiter(new EmailGuardOptions(), new TestClock());

        for (var i = 0; i < 200; i++)
            Consume(limiter, $"attendee{i}@example.test").Should().Be(EmailLimitScope.None);

        for (var i = 0; i < 200; i++)
            Consume(limiter, $"attendee{i}@example.test").Should().Be(EmailLimitScope.None);

        limiter
            .TryConsume(EmailKind.PasswordReset, "member@example.test")
            .Scope.Should()
            .Be(EmailLimitScope.None);
    }

    [Fact]
    public void TryConsume_ShippedDefaults_LetAGuardianReceiveEveryHouseholdNotification()
    {
        var clock = new TestClock();
        var limiter = new EmailSendLimiter(new EmailGuardOptions(), clock);

        for (var i = 0; i < 12; i++)
            Consume(limiter, "guardian@example.test").Should().Be(EmailLimitScope.None);

        clock.UtcNow = clock.UtcNow.AddHours(1);

        for (var i = 0; i < 12; i++)
            Consume(limiter, "guardian@example.test").Should().Be(EmailLimitScope.None);
    }

    [Fact]
    public void TryConsume_FromManyThreads_GrantsExactlyTheBurst()
    {
        var options = Options();
        options.GlobalBurst = 1000;
        options.GlobalPerHour = 1000;
        var limiter = new EmailSendLimiter(options, new TestClock());

        var granted = 0;
        Parallel.For(
            0,
            200,
            _ =>
            {
                if (Consume(limiter, Recipient) == EmailLimitScope.None)
                    Interlocked.Increment(ref granted);
            }
        );

        granted.Should().Be(options.RecipientBurst);
    }
}
