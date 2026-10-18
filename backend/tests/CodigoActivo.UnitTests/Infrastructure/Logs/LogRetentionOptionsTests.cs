using AwesomeAssertions;
using CodigoActivo.Infrastructure.Logs;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Logs;

public sealed class LogRetentionOptionsTests
{
    private static readonly LogRetentionOptions Options = new()
    {
        Diagnostics = TimeSpan.FromDays(7),
        Audit = TimeSpan.FromDays(90),
    };

    [Fact]
    public void OfGivesEachTableItsOwnRetention()
    {
        Options.Of(LogTable.Diagnostics).Should().Be(TimeSpan.FromDays(7));
        Options.Of(LogTable.Audit).Should().Be(TimeSpan.FromDays(90));
    }

    [Fact]
    public void OfRefusesATableThatIsNotALogTable()
    {
        var act = () => Options.Of(new LogTable("users"));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DefaultsAreThirtyAndSixtyDays()
    {
        var options = new LogRetentionOptions();

        options.Diagnostics.Should().Be(TimeSpan.FromDays(30));
        options.Audit.Should().Be(TimeSpan.FromDays(60));
        options.IsWithinLimits().Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 730, true)]
    [InlineData(0.5, 60, false)]
    [InlineData(30, 731, false)]
    public void IsWithinLimitsFromOneDayToTwoYears(double diagnostics, double audit, bool expected)
    {
        new LogRetentionOptions
        {
            Diagnostics = TimeSpan.FromDays(diagnostics),
            Audit = TimeSpan.FromDays(audit),
        }
            .IsWithinLimits()
            .Should()
            .Be(expected);
    }
}
