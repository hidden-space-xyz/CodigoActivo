using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailQueueOptionsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 30)]
    [InlineData(4, 120)]
    public void RetryDelayAfterEachFailureGrowsThroughTheConfiguredSchedule(
        int attempts,
        int expectedMinutes
    )
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
    [InlineData(0, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void IsExhaustedOnlyTheFifthFailureEndsTheAttempts(int attempts, bool expected)
    {
        EmailQueueOptions.IsExhausted(attempts).Should().Be(expected);
    }

    [Fact]
    public void RetryDelaysAlwaysCoverEveryAttemptButTheLast()
    {
        EmailQueueOptions.RetryDelays.Should().HaveCount(EmailQueueOptions.MaxAttempts - 1);
        EmailQueueOptions.RetryDelays.Should().BeInAscendingOrder();
    }

    [Theory]
    [InlineData(EmailQueueOptions.DefaultBatchSize, EmailQueueOptions.DefaultWorkers, 60, 3, 210)]
    [InlineData(EmailQueueOptions.DefaultWorkers, EmailQueueOptions.DefaultWorkers, 60, 1, 90)]
    [InlineData(2, 8, 60, 1, 90)]
    [InlineData(EmailQueueOptions.MaxBatchSize, EmailQueueOptions.DefaultWorkers, 60, 25, 1530)]
    [InlineData(EmailQueueOptions.DefaultBatchSize, 1, 60, 10, 630)]
    [InlineData(EmailQueueOptions.MaxBatchSize, 1, 600, 100, 60030)]
    [InlineData(0, 0, 60, 1, 90)]
    [InlineData(EmailQueueOptions.DefaultBatchSize, EmailQueueOptions.DefaultWorkers, 0, 3, 30)]
    public void LeaseCoversEveryDeliveryWaveOfTheClaimedBatch(
        int batchSize,
        int workers,
        int sendTimeoutSeconds,
        int expectedWaves,
        int expectedLeaseSeconds
    )
    {
        var options = new EmailQueueOptions
        {
            BatchSize = batchSize,
            Workers = workers,
            SendTimeout = TimeSpan.FromSeconds(sendTimeoutSeconds),
        };

        options.DeliveryWaves.Should().Be(expectedWaves);
        options
            .Lease.Should()
            .Be(TimeSpan.FromSeconds(expectedLeaseSeconds))
            .And.BeGreaterThan(
                expectedWaves * options.SendTimeout,
                "a claimed row may not become claimable again while its own worker can still be sending it"
            );
    }

    [Fact]
    public void LeaseWithTheSlowestConfigurationStaysUnderSeventeenHours()
    {
        var options = new EmailQueueOptions
        {
            BatchSize = EmailQueueOptions.MaxBatchSize,
            Workers = 1,
            SendTimeout = EmailQueueOptions.MaxSendTimeout,
        };

        options.Lease.Should().BeLessThan(TimeSpan.FromHours(17));
    }

    [Theory]
    [InlineData(EmailKind.TwoFactorCode, EmailQueueOptions.CriticalPriority)]
    [InlineData(EmailKind.PasswordReset, EmailQueueOptions.CriticalPriority)]
    [InlineData(EmailKind.AccountVerification, EmailQueueOptions.CriticalPriority)]
    [InlineData(EmailKind.SecurityAlert, EmailQueueOptions.StandardPriority)]
    [InlineData(EmailKind.ActivityNotification, EmailQueueOptions.StandardPriority)]
    [InlineData(EmailKind.Manual, EmailQueueOptions.BulkPriority)]
    public void PriorityOfInteractiveMailLeavesBeforeAutomaticAndBulkMail(
        EmailKind kind,
        int expected
    )
    {
        EmailQueueOptions.PriorityOf(kind).Should().Be(expected);
    }

    [Theory]
    [InlineData(EmailKind.TwoFactorCode, true)]
    [InlineData(EmailKind.PasswordReset, true)]
    [InlineData(EmailKind.AccountVerification, true)]
    [InlineData(EmailKind.SecurityAlert, false)]
    [InlineData(EmailKind.ActivityNotification, false)]
    [InlineData(EmailKind.Manual, false)]
    public void IsCapacityExemptOnlyTheMailSomebodyIsWaitingFor(EmailKind kind, bool expected)
    {
        EmailQueueOptions.IsCapacityExempt(kind).Should().Be(expected);
    }

    [Fact]
    public void PrioritiesAlwaysRankCriticalMailFirstAndBulkMailLast()
    {
        EmailQueueOptions
            .CriticalPriority.Should()
            .BeLessThan(EmailQueueOptions.StandardPriority)
            .And.BeLessThan(EmailQueueOptions.BulkPriority);
        EmailQueueOptions.StandardPriority.Should().BeLessThan(EmailQueueOptions.BulkPriority);
    }

    [Fact]
    public void DefaultsMatchTheDocumentedQueuePolicy()
    {
        var options = new EmailQueueOptions();

        options.Capacity.Should().Be(1000);
        options.Workers.Should().Be(4);
        options.BatchSize.Should().Be(10);
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(5));
        options.SendTimeout.Should().Be(TimeSpan.FromSeconds(60));
        options.ShutdownDrain.Should().Be(TimeSpan.FromSeconds(20));
        EmailQueueOptions.MaxAttempts.Should().Be(5);
    }
}
