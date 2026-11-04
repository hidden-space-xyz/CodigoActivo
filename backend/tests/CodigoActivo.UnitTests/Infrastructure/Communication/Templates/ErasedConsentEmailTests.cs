using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Infrastructure.Communication.Templates;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class ErasedConsentEmailTests
{
    private static readonly ErasedConsent Consent = new(
        "Feria de verano",
        "Autorización de imagen",
        "Leo Ruiz",
        "Marta Ruiz",
        new DateTimeOffset(2026, 7, 4, 10, 30, 0, TimeSpan.Zero)
    );

    [Fact]
    public void CreateTellsTheLegalContactWhatWasErasedWhoGaveItAndWhatToStopDoing()
    {
        var message = ErasedConsentEmail.Create(
            "legal@asociacion.test",
            "Asociación",
            [Consent],
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message.Kind.Should().Be(EmailKind.ConsentWithdrawalNotice);
        message.ToAddress.Should().Be("legal@asociacion.test");
        message.ToName.Should().Be("Asociación");
        message.Subject.Should().Be(AppStrings.EmailsConsentErasedSubject);
        message
            .TextBody.Should()
            .Contain(
                "Al borrar una cuenta o un evento se ha borrado el consentimiento «Autorización de imagen» de Leo Ruiz para el evento «Feria de verano»."
            )
            .And.Contain("Lo dio Marta Ruiz el 04/07/2026, 10:30 h.")
            .And.Contain(AppStrings.EmailsConsentErasedAction)
            .And.Contain(AppStrings.EmailsConsentErasedBlockedNote);
        message
            .HtmlBody.Should()
            .Contain("de <b>Leo Ruiz</b> para el evento <b>«Feria de verano»</b>");
    }

    [Fact]
    public void CreateSeveralConsentsListsEachOneInThePlural()
    {
        var message = ErasedConsentEmail.Create(
            "legal@asociacion.test",
            "Asociación",
            [
                Consent,
                Consent with
                {
                    PersonName = "Ana Gil",
                    DocumentName = "Publicación del nombre",
                },
            ],
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message.Subject.Should().Be(AppStrings.EmailsConsentErasedSubjectMany(2));
        message
            .TextBody.Should()
            .Contain("«Autorización de imagen» de Leo Ruiz")
            .And.Contain("«Publicación del nombre» de Ana Gil")
            .And.Contain(AppStrings.EmailsConsentErasedActionMany)
            .And.Contain(AppStrings.EmailsConsentErasedBlockedNoteMany);
    }

    [Fact]
    public void CreateWithoutConsentsIsRefused()
    {
        var act = () =>
            ErasedConsentEmail.Create(
                "legal@asociacion.test",
                "Asociación",
                [],
                TimeZoneInfo.Utc,
                "https://app.test"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDatesTheConsentInTheTimeZoneOfTheSite()
    {
        var message = ErasedConsentEmail.Create(
            "legal@asociacion.test",
            "Asociación",
            [Consent],
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"),
            "https://app.test"
        );

        message.TextBody.Should().Contain("el 04/07/2026, 12:30 h.");
    }

    [Fact]
    public void CreateEncodesTheNamesInTheHtml()
    {
        var message = ErasedConsentEmail.Create(
            "legal@asociacion.test",
            "Asociación",
            [Consent with { PersonName = "<i>Leo</i>" }],
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message.HtmlBody.Should().Contain("&lt;i&gt;Leo&lt;/i&gt;").And.NotContain("<i>Leo</i>");
    }
}
