using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class ExpiringEmailTests
{
    private static readonly EmailRecipient Ana = new("ana@test.com", "Ana");
    private static readonly UserId AnaId = UserId.From(Guid.NewGuid());
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private readonly AccountEmailComposer sut = new(
        new ApplicationOptions { BaseUrl = "https://app.test" },
        new TestClock()
    );

    [Fact]
    public void CreateShortLifetimeRoundsUpToOneMinute()
    {
        var message = sut.LoginCode(Ana, "000000", TimeSpan.FromSeconds(20));

        message.TextBody.Should().Contain("1 minutos");
    }

    [Fact]
    public void CreateScriptInNameOrCodeHtmlEncodesThem()
    {
        var message = sut.LoginCode(
            new EmailRecipient("ana@test.com", "<script>alert(1)</script>"),
            "<b>1</b>",
            TenMinutes
        );

        message.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
        message.HtmlBody.Should().NotContain("<b>1</b>").And.Contain("&lt;b&gt;1&lt;/b&gt;");
    }

    [Fact]
    public void CreateLinkEmailsShowTheCodeOnlyInsideTheirLink()
    {
        EmailMessage[] messages =
        [
            sut.AccountVerification(Ana, AnaId, "123456", TenMinutes, newsConsent: false),
            sut.EmailChangeConfirmation(Ana, AnaId, "123456", TenMinutes),
            sut.PasswordReset(Ana, AnaId, "123456", TenMinutes),
        ];

        foreach (var message in messages)
        {
            message.Subject.Should().NotContain("123456");
            message.TextBody.Should().Contain("10 minutos");
            message
                .TextBody.Split('\n')
                .Where(line => !line.StartsWith("https://", StringComparison.Ordinal))
                .Should()
                .NotContain(
                    line => line.Contains("123456"),
                    "the code must not appear as copyable text outside the link"
                );
        }
    }

    [Theory]
    [InlineData(ConfirmationCodePurpose.AccountDeletion, "eliminación", "eliminar tu cuenta", true)]
    [InlineData(
        ConfirmationCodePurpose.EmailChange,
        "cambio de correo",
        "cambiar tu correo",
        false
    )]
    [InlineData(
        ConfirmationCodePurpose.AuthenticatorSetup,
        "app de autenticación",
        "configurar una aplicación de autenticación",
        false
    )]
    public void ConfirmationCodeNamesTheChangeInTheSubjectAndTheWarningNote(
        ConfirmationCodePurpose purpose,
        string subject,
        string warning,
        bool mentionsDependents
    )
    {
        var message = sut.ConfirmationCode(Ana, purpose, "482913", TenMinutes);

        message.Subject.Should().Contain(subject).And.NotContain("482913");
        message
            .TextBody.Should()
            .Contain("\n482913\n")
            .And.Contain("10 minutos")
            .And.Contain($"Si no has solicitado {warning}");
        message.TextBody.Contains("menores a tu cargo").Should().Be(mentionsDependents);
        message
            .HtmlBody.Should()
            .Contain(">482913<")
            .And.NotContain("href=\"https://app.test/login");
    }

    [Fact]
    public void ConfirmationCodeUnknownPurposeThrows()
    {
        var act = () =>
            sut.ConfirmationCode(Ana, (ConfirmationCodePurpose)99, "482913", TenMinutes);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BlockedDataConsultationCodeTellsWhoDidNotAskForItWhatToDo()
    {
        var message = sut.BlockedDataConsultationCode(Ana, "482913", TenMinutes);

        message.TextBody.Should().Contain("Si no has solicitado esta consulta");
        message
            .HtmlBody.Should()
            .Contain(">482913<")
            .And.NotContain("href=\"https://app.test/login");
    }
}
