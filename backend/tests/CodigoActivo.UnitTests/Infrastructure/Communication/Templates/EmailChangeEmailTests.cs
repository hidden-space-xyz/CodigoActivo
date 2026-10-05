using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Infrastructure.Communication.Templates;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class EmailChangeEmailTests
{
    private const string ConfirmUrl = "https://app.test/confirm-email#userId=abc&code=123456";
    private const string SiteUrl = "https://app.test";

    private static EmailMessage Create()
    {
        return EmailChangeEmail.Create(
            "new@test.com",
            string.Empty,
            ConfirmUrl,
            SiteUrl,
            TimeSpan.FromMinutes(15)
        );
    }

    [Fact]
    public void CreateAddressesTheNewEmailWithTheLinkItsLifetimeAndHowToIgnoreIt()
    {
        var message = Create();

        message.Kind.Should().Be(EmailKind.AccountVerification);
        message.ToAddress.Should().Be("new@test.com");
        message.Subject.Should().Be(AppStrings.EmailsEmailChangeSubject);
        message
            .TextBody.Should()
            .Contain(ConfirmUrl)
            .And.Contain("15 minutos")
            .And.Contain(AppStrings.EmailsEmailChangeIgnoreNote);
        message.HtmlBody.Should().Contain("confirm-email#userId=abc").And.Contain("code=123456");
    }

    [Fact]
    public void CreateShowsTheCodeOnlyInsideTheLink()
    {
        var message = Create();

        message.Subject.Should().NotContain("123456");
        message
            .TextBody.Replace(ConfirmUrl, string.Empty, StringComparison.Ordinal)
            .Should()
            .NotContain("123456", "the code must not appear as copyable text outside the link");
    }
}
