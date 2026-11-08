using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Infrastructure.Communication.Templates;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class ConfirmationCodeEmailTests
{
    private const string SiteUrl = "https://app.test";

    [Fact]
    public void CreateValidRequestAddressesRecipientWithCodeAndLifetime()
    {
        var message = ConfirmationCodeEmail.Create(
            "ana@test.com",
            "Ana",
            ConfirmationCodePurpose.AccountDeletion,
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
        var message = ConfirmationCodeEmail.Create(
            "ana@test.com",
            "Ana",
            ConfirmationCodePurpose.AccountDeletion,
            "000000",
            SiteUrl,
            TimeSpan.FromSeconds(20)
        );

        message.TextBody.Should().Contain("1 minutos");
    }

    [Fact]
    public void CreateScriptInNameOrCodeHtmlEncodesThem()
    {
        var message = ConfirmationCodeEmail.Create(
            "ana@test.com",
            "<script>alert(1)</script>",
            ConfirmationCodePurpose.AccountDeletion,
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
        var message = ConfirmationCodeEmail.Create(
            "ana@test.com",
            "Ana",
            ConfirmationCodePurpose.AccountDeletion,
            "482913",
            SiteUrl,
            TimeSpan.FromMinutes(10)
        );

        message.TextBody.Should().Contain("Si no has solicitado eliminar tu cuenta");
    }

    [Theory]
    [InlineData(ConfirmationCodePurpose.EmailChange, "cambio de correo", "cambiar tu correo")]
    [InlineData(
        ConfirmationCodePurpose.AuthenticatorSetup,
        "app de autenticación",
        "configurar una aplicación de autenticación"
    )]
    public void CreateOtherChangeNamesItInTheSubjectAndTheWarningNote(
        ConfirmationCodePurpose purpose,
        string subject,
        string warning
    )
    {
        var message = ConfirmationCodeEmail.Create(
            "ana@test.com",
            "Ana",
            purpose,
            "482913",
            SiteUrl,
            TimeSpan.FromMinutes(10)
        );

        message.Kind.Should().Be(EmailKind.TwoFactorCode);
        message.Subject.Should().Contain(subject).And.NotContain("482913");
        message
            .TextBody.Should()
            .Contain("\n482913\n")
            .And.Contain("10 minutos")
            .And.Contain($"Si no has solicitado {warning}")
            .And.NotContain("menores a tu cargo");
        message.HtmlBody.Should().Contain(">482913<");
    }

    [Fact]
    public void CreateUnknownPurposeThrows()
    {
        var act = () =>
            ConfirmationCodeEmail.Create(
                "ana@test.com",
                "Ana",
                (ConfirmationCodePurpose)99,
                "482913",
                SiteUrl,
                TimeSpan.FromMinutes(10)
            );

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
