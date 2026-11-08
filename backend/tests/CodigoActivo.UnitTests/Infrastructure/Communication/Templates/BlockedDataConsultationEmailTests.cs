using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Infrastructure.Communication.Templates;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class BlockedDataConsultationEmailTests
{
    private const string SiteUrl = "https://app.test";

    private static readonly OpenedConsultation Consultation = new(
        "Dana Delegada",
        "Juzgado <b>2</b>",
        "Diligencias 123/2026",
        new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero)
    );

    private static EmailMessage Create(params OpenedConsultation[] consultations)
    {
        return BlockedDataConsultationEmail.Create(
            "legal@test.com",
            "Asociación",
            consultations,
            TimeZoneInfo.Utc,
            SiteUrl
        );
    }

    [Fact]
    public void CreateOneConsultationNamesWhoAsksAsTheOfficerWroteIt()
    {
        var message = Create(Consultation);

        message.Kind.Should().Be(EmailKind.BlockedDataConsultationNotice);
        message.TextBody.Should().Contain("«Juzgado <b>2</b>»");
        message
            .HtmlBody.Should()
            .Contain("Juzgado &lt;b&gt;2&lt;/b&gt;")
            .And.NotContain("<b>2</b>");
    }

    [Fact]
    public void CreateSeveralConsultationsNamesWhoAsksInEach()
    {
        var other = Consultation with { Requester = "Persona reclamante" };

        var message = Create(Consultation, other);

        message.TextBody.Should().Contain("«Juzgado <b>2</b>»").And.Contain("«Persona reclamante»");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CreateNeverListsWhoTheDataMayBeHandedTo(int consultations)
    {
        var message = Create(Enumerable.Repeat(Consultation, consultations).ToArray());

        message
            .TextBody.Should()
            .Contain("Si no reconocéis")
            .And.NotContain("Ministerio Fiscal")
            .And.NotContain("autoridad de protección de datos");
    }
}
