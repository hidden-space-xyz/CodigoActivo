using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using CodigoActivo.Domain.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails;

public sealed class AccountDeletionCodeEmailTests
{
    private const string SiteUrl = "https://app.test";

    [Fact]
    public void CreateValidRequestAddressesRecipientWithCodeAndLifetime()
    {
        var message = AccountDeletionCodeEmail.Create(
            "ana@test.com",
            "Ana",
            "482913",
            SiteUrl,
            TimeSpan.FromMinutes(10)
        );

        message.Kind.Should().Be(EmailKind.TwoFactorCode);
        message.ToAddress.Should().Be("ana@test.com");
        message.ToName.Should().Be("Ana");
        message
            .Subject.Should()
            .NotContain("482913", "the code must not appear in the subject line");
        message.Subject.Should().Contain("eliminación");
        message
            .TextBody.Should()
            .Contain("Ana")
            .And.Contain("\n482913\n")
            .And.Contain("10 minutos");
        message.TextBody.Should().Contain("menores a tu cargo");
        message.HtmlBody.Should().Contain("Ana").And.Contain(">482913<").And.Contain("10 minutos");
        message
            .HtmlBody.Should()
            .NotContain(
                "href=\"https://app.test/login",
                "the email carries a code, never a link that deletes anything"
            );
    }

    [Fact]
    public void CreateShortLifetimeRoundsUpToOneMinute()
    {
        var message = AccountDeletionCodeEmail.Create(
            "ana@test.com",
            "Ana",
            "000000",
            SiteUrl,
            TimeSpan.FromSeconds(20)
        );

        message.TextBody.Should().Contain("1 minutos");
    }

    [Fact]
    public void CreateScriptInNameOrCodeHtmlEncodesThem()
    {
        var message = AccountDeletionCodeEmail.Create(
            "ana@test.com",
            "<script>alert(1)</script>",
            "<b>1</b>",
            SiteUrl,
            TimeSpan.FromMinutes(10)
        );

        message.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
        message.HtmlBody.Should().NotContain("<b>1</b>").And.Contain("&lt;b&gt;1&lt;/b&gt;");
    }

    [Fact]
    public void CreateWarningNoteTellsTheReaderWhatToDoWhenTheyDidNotAskForIt()
    {
        var message = AccountDeletionCodeEmail.Create(
            "ana@test.com",
            "Ana",
            "482913",
            SiteUrl,
            TimeSpan.FromMinutes(10)
        );

        message.TextBody.Should().Contain("Si no has solicitado eliminar tu cuenta");
    }
}
