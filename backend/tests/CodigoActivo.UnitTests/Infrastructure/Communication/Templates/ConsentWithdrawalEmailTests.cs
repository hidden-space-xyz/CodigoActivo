using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Infrastructure.Communication.Templates;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class ConsentWithdrawalEmailTests
{
    private static readonly ConsentWithdrawal Withdrawal = new(
        "Feria de verano",
        "Autorización de imagen",
        "Leo Ruiz",
        "Marta Ruiz",
        new DateTimeOffset(2026, 7, 4, 10, 30, 0, TimeSpan.Zero)
    );

    [Fact]
    public void CreateTellsTheLegalContactWhatWasWithdrawnWhoRecordedItAndWhatToStopDoing()
    {
        var message = ConsentWithdrawalEmail.Create(
            "legal@asociacion.test",
            "Asociación",
            Withdrawal,
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message.Kind.Should().Be(EmailKind.ConsentWithdrawalNotice);
        message.ToAddress.Should().Be("legal@asociacion.test");
        message.ToName.Should().Be("Asociación");
        message.Subject.Should().Be(AppStrings.EmailsConsentWithdrawnSubject);
        message
            .TextBody.Should()
            .Contain(
                "Se ha retirado el consentimiento «Autorización de imagen» de Leo Ruiz para el evento «Feria de verano»."
            )
            .And.Contain("Lo registró Marta Ruiz el 04/07/2026, 10:30 h.")
            .And.Contain(AppStrings.EmailsConsentWithdrawnAction)
            .And.Contain(AppStrings.EmailsConsentWithdrawnHistoryNote);
        message
            .HtmlBody.Should()
            .Contain("de <b>Leo Ruiz</b> para el evento <b>«Feria de verano»</b>");
    }

    [Fact]
    public void CreateDatesTheWithdrawalInTheTimeZoneOfTheSite()
    {
        var message = ConsentWithdrawalEmail.Create(
            "legal@asociacion.test",
            "Asociación",
            Withdrawal,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"),
            "https://app.test"
        );

        message.TextBody.Should().Contain("el 04/07/2026, 12:30 h.");
    }

    [Fact]
    public void CreateEncodesTheNamesInTheHtml()
    {
        var message = ConsentWithdrawalEmail.Create(
            "legal@asociacion.test",
            "Asociación",
            Withdrawal with
            {
                PersonName = "<i>Leo</i>",
            },
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message.HtmlBody.Should().Contain("&lt;i&gt;Leo&lt;/i&gt;").And.NotContain("<i>Leo</i>");
    }
}
