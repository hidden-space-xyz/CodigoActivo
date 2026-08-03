using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails;

public sealed class ActivitySignupDecisionEmailTests
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
    public void Confirmed_MarkupInParticipantName_EncodesTheHtmlBodyAndLeavesTheTextBodyIntact()
    {
        var message = ActivitySignupDecisionEmail.Confirmed(
            "ada@test.com",
            "Ada",
            "<script>alert(1)</script>",
            "Participante",
            Details,
            TimeZoneInfo.Utc
        );

        message.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
        message.TextBody.Should().Contain("la inscripción de <script>alert(1)</script>");
    }

    [Fact]
    public void Denied_NoParticipantName_AddressesTheRecipientsOwnSignupAndOmitsRoleAndNote()
    {
        var message = ActivitySignupDecisionEmail.Denied(
            "ada@test.com",
            "Ada",
            null,
            Details,
            TimeZoneInfo.Utc
        );

        message.Subject.Should().Be("Inscripción rechazada: Taller de robótica");
        message
            .TextBody.Should()
            .Contain("tu inscripción")
            .And.NotContain("Participa como")
            .And.NotContain("\n\n\n");
        message.HtmlBody.Should().NotContain("<p></p>");
    }
}
