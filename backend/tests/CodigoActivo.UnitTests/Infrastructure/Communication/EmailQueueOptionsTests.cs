using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailQueueOptionsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    [InlineData(40, 60)]
    public void RetryDelayAfterEachFailureGrowsUpToAnHour(int attempts, int expectedMinutes)
    {
        EmailQueueOptions
            .RetryDelayAfter(attempts)
            .Should()
            .Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(9)]
    public void RetryDelayAfterOutsideTheScheduleStaysWithinItsBounds(int attempts)
    {
        var delay = EmailQueueOptions.RetryDelayAfter(attempts);

        delay
            .Should()
            .BeGreaterThanOrEqualTo(EmailQueueOptions.RetryDelays[0])
            .And.BeLessThanOrEqualTo(EmailQueueOptions.RetryDelays[^1]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 5)]
    [InlineData(4, 10)]
    [InlineData(5, 30)]
    [InlineData(50, 30)]
    public void PauseDelayAfterEachServerFailureGrowsUpToHalfAnHour(
        int failures,
        int expectedMinutes
    )
    {
        EmailQueueOptions
            .PauseDelayAfter(failures)
            .Should()
            .Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void DelaySchedulesAlwaysGrow()
    {
        EmailQueueOptions.RetryDelays.Should().BeInAscendingOrder();
        EmailQueueOptions.PauseDelays.Should().BeInAscendingOrder();
    }

    [Theory]
    [InlineData(EmailQueueOptions.DefaultBatchSize, 60, 630)]
    [InlineData(1, 60, 90)]
    [InlineData(0, 60, 90)]
    [InlineData(100, 600, 60030)]
    [InlineData(EmailQueueOptions.DefaultBatchSize, 0, 30)]
    public void LeaseCoversEverySendOfTheClaimedBatch(
        int batchSize,
        int sendTimeoutSeconds,
        int expectedLeaseSeconds
    )
    {
        var options = new EmailQueueOptions
        {
            BatchSize = batchSize,
            SendTimeout = TimeSpan.FromSeconds(sendTimeoutSeconds),
        };

        options
            .Lease.Should()
            .Be(TimeSpan.FromSeconds(expectedLeaseSeconds))
            .And.BeGreaterThan(
                Math.Max(batchSize, 1) * options.SendTimeout,
                "a claimed row may not become claimable again while its own worker can still be sending it"
            );
    }

    [Theory]
    [InlineData(EmailKind.TwoFactorCode, EmailQueueOptions.IdentifiedCodePriority)]
    [InlineData(EmailKind.AccessRecovery, EmailQueueOptions.AnonymousCodePriority)]
    [InlineData(EmailKind.AccountVerification, EmailQueueOptions.AnonymousCodePriority)]
    [InlineData(EmailKind.SecurityAlert, EmailQueueOptions.NoticePriority)]
    [InlineData(EmailKind.ActivityNotification, EmailQueueOptions.NoticePriority)]
    [InlineData(EmailKind.ComingOfAgeNotice, EmailQueueOptions.NoticePriority)]
    [InlineData(EmailKind.ConsentWithdrawalNotice, EmailQueueOptions.NoticePriority)]
    [InlineData(EmailKind.BlockedDataConsultationNotice, EmailQueueOptions.NoticePriority)]
    [InlineData(EmailKind.Manual, EmailQueueOptions.BulkPriority)]
    public void PriorityOfLoginCodesLeaveBeforeAnonymousCodesNoticesAndBulkMail(
        EmailKind kind,
        int expected
    )
    {
        EmailQueueOptions.PriorityOf(kind).Should().Be(expected);
    }

    [Fact]
    public void PriorityOfEveryKindKeepsTheBulkPriorityForManualMailOnly()
    {
        Enum.GetValues<EmailKind>()
            .Where(kind => EmailQueueOptions.PriorityOf(kind) == EmailQueueOptions.BulkPriority)
            .Should()
            .Equal(EmailKind.Manual);
    }

    [Fact]
    public void PrioritiesAlwaysRankLoginCodesFirstAndBulkMailLast()
    {
        int[] priorities =
        [
            EmailQueueOptions.IdentifiedCodePriority,
            EmailQueueOptions.AnonymousCodePriority,
            EmailQueueOptions.NoticePriority,
            EmailQueueOptions.BulkPriority,
        ];

        priorities.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(EmailKind.TwoFactorCode, true)]
    [InlineData(EmailKind.AccessRecovery, true)]
    [InlineData(EmailKind.AccountVerification, true)]
    [InlineData(EmailKind.SecurityAlert, false)]
    [InlineData(EmailKind.ActivityNotification, false)]
    [InlineData(EmailKind.ComingOfAgeNotice, false)]
    [InlineData(EmailKind.ConsentWithdrawalNotice, false)]
    [InlineData(EmailKind.BlockedDataConsultationNotice, false)]
    [InlineData(EmailKind.Manual, false)]
    public void IsGuardedOnlyTheCodesSomebodyAsksFor(EmailKind kind, bool expected)
    {
        EmailQueueOptions.IsGuarded(kind).Should().Be(expected);
    }

    [Fact]
    public void IsCappedOnlyAdministratorWrittenMail()
    {
        Enum.GetValues<EmailKind>()
            .Where(EmailQueueOptions.IsCapped)
            .Should()
            .Equal(EmailKind.Manual);
    }

    [Theory]
    [InlineData(EmailKind.ComingOfAgeNotice)]
    [InlineData(EmailKind.ConsentWithdrawalNotice)]
    [InlineData(EmailKind.BlockedDataConsultationNotice)]
    public void LifetimeOfTheNoticesThatMustArriveNeverEnds(EmailKind kind)
    {
        EmailQueueOptions.LifetimeOf(kind).Should().BeNull();
    }

    [Theory]
    [InlineData(EmailKind.TwoFactorCode)]
    [InlineData(EmailKind.AccessRecovery)]
    [InlineData(EmailKind.AccountVerification)]
    [InlineData(EmailKind.SecurityAlert)]
    [InlineData(EmailKind.ActivityNotification)]
    [InlineData(EmailKind.Manual)]
    public void LifetimeOfEveryOtherKindIsThreeDays(EmailKind kind)
    {
        EmailQueueOptions.LifetimeOf(kind).Should().Be(TimeSpan.FromDays(3));
    }

    [Theory]
    [InlineData(0, EmailQueueOptions.BulkPriority)]
    [InlineData(799, EmailQueueOptions.BulkPriority)]
    [InlineData(800, EmailQueueOptions.AnonymousCodePriority)]
    [InlineData(899, EmailQueueOptions.AnonymousCodePriority)]
    [InlineData(900, EmailQueueOptions.IdentifiedCodePriority)]
    [InlineData(999, EmailQueueOptions.IdentifiedCodePriority)]
    public void ClaimablePriorityDailyLimitKeepsItsLastShareForLoginCodes(long sent, int expected)
    {
        EmailQueueOptions.ClaimablePriority(sent, dailyLimit: 1000).Should().Be(expected);
    }

    [Fact]
    public void ClaimablePriorityDailyLimitSpentClaimsNothing()
    {
        EmailQueueOptions.ClaimablePriority(1000, dailyLimit: 1000).Should().BeNull();
    }

    [Theory]
    [InlineData(EmailQueueOptions.IdentifiedCodePriority, false)]
    [InlineData(EmailQueueOptions.AnonymousCodePriority, false)]
    [InlineData(EmailQueueOptions.NoticePriority, true)]
    [InlineData(EmailQueueOptions.BulkPriority, true)]
    public void IsPacedPerRecipientOnlyNoticesAndBulkMail(int priority, bool expected)
    {
        EmailQueueOptions.IsPacedPerRecipient(priority).Should().Be(expected);
    }

    [Fact]
    public void DefaultsMatchTheDocumentedQueuePolicy()
    {
        var options = new EmailQueueOptions();

        options.Capacity.Should().Be(2000);
        options.BatchSize.Should().Be(10);
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(5));
        options.SendTimeout.Should().Be(TimeSpan.FromSeconds(60));
        options.ShutdownDrain.Should().Be(TimeSpan.FromSeconds(20));
        EmailQueueOptions.FailuresBeforePause.Should().Be(3);
        EmailQueueOptions.OverdueAge.Should().Be(TimeSpan.FromHours(24));
        EmailQueueOptions.DailyWindow.Should().Be(TimeSpan.FromHours(24));
    }
}
