using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails;

public sealed class ActivitySignupEmailTests
{
    private static readonly ActivityEmailDetails Details = new(
        "Taller de robótica",
        "Semana de la Ciencia",
        "Sala A",
        new DateTimeOffset(2026, 7, 20, 16, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 20, 18, 30, 0, TimeSpan.Zero),
        "https://app.test/events/e6c9"
    );

    [Fact]
    public void Create_MarkupInNames_EncodesTheHtmlBodyAndLeavesTheTextBodyIntact()
    {
        var message = ActivitySignupEmail.Create(
            "ada@test.com",
            "<b>Ada</b>",
            Details,
            [new ActivitySignupParticipant("<script>alert(1)</script>", "Voluntario")],
            TimeZoneInfo.Utc,
            "https://app.test/account",
            "https://app.test"
        );

        message.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
        message.HtmlBody.Should().NotContain("<b>Ada</b>").And.Contain("&lt;b&gt;Ada&lt;/b&gt;");
        message.TextBody.Should().Contain("<script>alert(1)</script> (Voluntario)");
    }
}
