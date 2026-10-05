using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Infrastructure.Communication.Templates;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class EmailInUseEmailTests
{
    private const string LoginUrl = "https://app.test/login";

    [Fact]
    public void CreateTellsTheHolderNothingChangedAndHowToSignIn()
    {
        var message = EmailInUseEmail.Create("ana@test.com", "Ana", LoginUrl, "https://app.test");

        message.Kind.Should().Be(EmailKind.AccountVerification);
        message.ToAddress.Should().Be("ana@test.com");
        message.Subject.Should().Be(AppStrings.EmailsEmailInUseSubject);
        message
            .TextBody.Should()
            .Contain(AppStrings.EmailsEmailInUseIntroText)
            .And.Contain(LoginUrl)
            .And.Contain(AppStrings.EmailsEmailInUseRecovery)
            .And.Contain(AppStrings.EmailsEmailInUseIgnoreNote)
            .And.NotContain("code=");
        message.HtmlBody.Should().Contain($"href=\"{LoginUrl}\"");
    }
}
