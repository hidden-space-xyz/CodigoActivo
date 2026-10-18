using AwesomeAssertions;
using CodigoActivo.Infrastructure.Logs;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Logs;

public sealed class LogPartitionsTests
{
    private static readonly DateTimeOffset Now = new(
        2026,
        10,
        17,
        22,
        30,
        0,
        TimeSpan.FromHours(2)
    );

    [Fact]
    public void NameOfUsesTheTableAndTheUtcDay()
    {
        LogPartitions
            .NameOf(LogTable.Diagnostics, new DateOnly(2026, 10, 7))
            .Should()
            .Be("diagnostic_log_entries_p20261007");
    }

    [Fact]
    public void DayOfPlacesAnInstantInItsUtcDay()
    {
        LogPartitions
            .DayOf(new DateTimeOffset(2026, 10, 18, 1, 0, 0, TimeSpan.FromHours(2)))
            .Should()
            .Be(new DateOnly(2026, 10, 17));
    }

    [Fact]
    public void TryParseDayReadsOnlyThePartitionsOfTheTable()
    {
        LogPartitions
            .TryParseDay(LogTable.Audit, "audit_log_entries_p20260704", out var day)
            .Should()
            .BeTrue();
        day.Should().Be(new DateOnly(2026, 7, 4));

        LogPartitions
            .TryParseDay(LogTable.Audit, "diagnostic_log_entries_p20260704", out _)
            .Should()
            .BeFalse();
        LogPartitions
            .TryParseDay(LogTable.Audit, "audit_log_entries_default", out _)
            .Should()
            .BeFalse();
        LogPartitions
            .TryParseDay(LogTable.Audit, "audit_log_entries_p20261341", out _)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void CutoffOfIsOneMaintenanceIntervalYoungerThanTheRetention()
    {
        LogPartitions
            .CutoffOf(TimeSpan.FromDays(30), Now)
            .Should()
            .Be(new DateTimeOffset(2026, 9, 17, 21, 30, 0, TimeSpan.Zero));
        LogPartitions
            .CutoffOf(TimeSpan.FromDays(60), Now)
            .Should()
            .Be(new DateTimeOffset(2026, 8, 18, 21, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void UpcomingDaysAreTodayAndTheTwoNextUtcDays()
    {
        LogPartitions
            .UpcomingDays(Now)
            .Should()
            .Equal(
                new DateOnly(2026, 10, 17),
                new DateOnly(2026, 10, 18),
                new DateOnly(2026, 10, 19)
            );
    }

    [Fact]
    public void IsExpiredOnlyWhenTheWholeDayIsBeforeTheCutoff()
    {
        var cutoff = new DateTimeOffset(2026, 9, 17, 21, 30, 0, TimeSpan.Zero);

        LogPartitions.IsExpired(new DateOnly(2026, 9, 16), cutoff).Should().BeTrue();
        LogPartitions.IsExpired(new DateOnly(2026, 9, 17), cutoff).Should().BeFalse();
        LogPartitions
            .IsExpired(new DateOnly(2026, 9, 17), cutoff.AddHours(3))
            .Should()
            .BeTrue("the day ends exactly at the cutoff");
    }

    [Fact]
    public void IsWithinWindowFromTheCutoffDayToTheLastDayKeptReady()
    {
        LogPartitions
            .IsWithinWindow(TimeSpan.FromDays(30), new DateOnly(2026, 9, 16), Now)
            .Should()
            .BeFalse();
        LogPartitions
            .IsWithinWindow(TimeSpan.FromDays(30), new DateOnly(2026, 9, 17), Now)
            .Should()
            .BeTrue();
        LogPartitions
            .IsWithinWindow(TimeSpan.FromDays(30), new DateOnly(2026, 10, 19), Now)
            .Should()
            .BeTrue();
        LogPartitions
            .IsWithinWindow(TimeSpan.FromDays(30), new DateOnly(2026, 10, 20), Now)
            .Should()
            .BeFalse();
        LogPartitions
            .IsWithinWindow(TimeSpan.FromDays(60), new DateOnly(2026, 8, 18), Now)
            .Should()
            .BeTrue();
    }
}
