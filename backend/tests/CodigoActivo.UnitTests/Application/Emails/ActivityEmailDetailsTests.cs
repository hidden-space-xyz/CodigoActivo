using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails;

public sealed class ActivityEmailDetailsTests
{
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone(
        "Test/PlusTwo",
        TimeSpan.FromHours(2),
        "Test +02:00",
        "Test +02:00"
    );

    private static ActivityEmailDetails Details(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string activityTitle = "Taller de robótica"
    )
    {
        return new ActivityEmailDetails(
            activityTitle,
            "Semana de la Ciencia",
            "Sala A",
            startsAt,
            endsAt,
            "https://app.test/events/e6c9"
        );
    }

    [Fact]
    public void ScheduleText_SameLocalDay_RendersOneDateAndATimeRange()
    {
        var details = Details(
            new DateTimeOffset(2026, 7, 20, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 20, 18, 30, 0, TimeSpan.Zero)
        );

        details.ScheduleText(PlusTwo).Should().Be("20/07/2026, de 18:00 a 20:30 h");
    }

    [Fact]
    public void ScheduleText_CrossesLocalMidnight_RendersBothDates()
    {
        var details = Details(
            new DateTimeOffset(2026, 7, 20, 21, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 20, 23, 30, 0, TimeSpan.Zero)
        );

        details
            .ScheduleText(PlusTwo)
            .Should()
            .Be("del 20/07/2026 a las 23:00 h al 21/07/2026 a las 01:30 h");
    }

    [Fact]
    public void ToTextBlock_RoleProvided_AppendsTheRoleRow()
    {
        var details = Details(
            new DateTimeOffset(2026, 7, 20, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 20, 18, 30, 0, TimeSpan.Zero)
        );

        details
            .ToTextBlock(TimeZoneInfo.Utc, "Voluntario")
            .Should()
            .Contain("Participa como: Voluntario");
        details.ToTextBlock(TimeZoneInfo.Utc).Should().NotContain("Participa como");
    }

    [Fact]
    public void ToHtmlBlock_MarkupInAValue_IsHtmlEncoded()
    {
        var details = Details(
            new DateTimeOffset(2026, 7, 20, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 20, 18, 30, 0, TimeSpan.Zero),
            "<script>alert(1)</script>"
        );

        details
            .ToHtmlBlock(TimeZoneInfo.Utc)
            .Should()
            .NotContain("<script>")
            .And.Contain("&lt;script&gt;");
    }
}
