using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Infrastructure.Communication.Templates;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class ComingOfAgeNoticeEmailTests
{
    private const string AccountUrl = "https://app.test/account";

    [Fact]
    public void CreateTellsTheGuardianWhenTheDependentIsErasedAndWhereToDownloadItsData()
    {
        var message = ComingOfAgeNoticeEmail.Create(
            "marta@test.com",
            "Marta",
            "Leo Ruiz",
            new DateOnly(2026, 8, 3),
            AccountUrl,
            "https://app.test"
        );

        message.Kind.Should().Be(EmailKind.ComingOfAgeNotice);
        message.ToAddress.Should().Be("marta@test.com");
        message.ToName.Should().Be("Marta");
        message.Subject.Should().Be(AppStrings.EmailsComingOfAgeSubject);
        message
            .TextBody.Should()
            .Contain("Leo Ruiz cumple 18 años el 03/08/2026")
            .And.Contain(AppStrings.EmailsComingOfAgeDownload)
            .And.Contain(AccountUrl)
            .And.Contain(AppStrings.EmailsComingOfAgeOwnAccount)
            .And.Contain(AppStrings.EmailsComingOfAgeBlockedCopyNote);
        message
            .HtmlBody.Should()
            .Contain("<b>Leo Ruiz</b> cumple 18 años el <b>03/08/2026</b>")
            .And.Contain($"href=\"{AccountUrl}\"");
    }

    [Fact]
    public void CreateEncodesTheNameOfTheDependentInTheHtml()
    {
        var message = ComingOfAgeNoticeEmail.Create(
            "marta@test.com",
            "Marta",
            "<i>Leo</i>",
            new DateOnly(2026, 8, 3),
            AccountUrl,
            "https://app.test"
        );

        message.HtmlBody.Should().Contain("&lt;i&gt;Leo&lt;/i&gt;").And.NotContain("<i>Leo</i>");
    }
}
