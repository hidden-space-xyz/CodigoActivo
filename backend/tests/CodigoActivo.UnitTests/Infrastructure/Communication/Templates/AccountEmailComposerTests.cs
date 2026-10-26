using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class AccountEmailComposerTests
{
    private static readonly EmailRecipient Ana = new("ana@test.com", "Ana");
    private static readonly UserId AnaId = UserId.From(
        Guid.Parse("3f2b8c4e-1d5a-4e7b-9c6f-0a1b2c3d4e5f")
    );
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private static readonly string SiteFooter =
        $"{AppStrings.EmailsFooterTagline}\nhttps://app.test";

    private readonly TestClock clock = new();
    private readonly AccountEmailComposer sut;

    public AccountEmailComposerTests()
    {
        sut = new AccountEmailComposer(
            new ApplicationOptions { BaseUrl = "https://app.test/" },
            clock
        );
    }

    [Fact]
    public void AccountVerificationTrailingSlashBaseUrlLinksTheVerificationPage()
    {
        var message = sut.AccountVerification(Ana, AnaId, "the-code", Lifetime, newsConsent: false);

        message.Kind.Should().Be(EmailKind.AccountVerification);
        message.ToAddress.Should().Be("ana@test.com");
        message.ToName.Should().Be("Ana");
        message.Subject.Should().NotContain("the-code");
        message
            .TextBody.Should()
            .Contain($"https://app.test/verify-account#userId={AnaId}&code=the-code")
            .And.Contain("15 minutos");
        message.HtmlBody.Should().Contain($"https://app.test/verify-account#userId={AnaId}");
        message.TextBody.Should().NotContain("novedades");
    }

    [Fact]
    public void AccountVerificationOfARegistrationThatAcceptedNewsSaysSo()
    {
        var message = sut.AccountVerification(Ana, AnaId, "the-code", Lifetime, newsConsent: true);

        message.TextBody.Should().Contain("aceptaste recibir novedades");
        message.HtmlBody.Should().Contain("aceptaste recibir novedades");
    }

    [Fact]
    public void EmailChangeConfirmationUnnamedRecipientLinksTheConfirmationPage()
    {
        var message = sut.EmailChangeConfirmation(
            new EmailRecipient("new@test.com", string.Empty),
            AnaId,
            "the-code",
            Lifetime
        );

        message.Kind.Should().Be(EmailKind.AccountVerification);
        message.ToAddress.Should().Be("new@test.com");
        message.ToName.Should().BeEmpty();
        message
            .TextBody.Should()
            .Contain($"https://app.test/confirm-email#userId={AnaId}&code=the-code");
    }

    [Fact]
    public void PasswordResetReservedCharactersInCodeAreEscapedInTheLink()
    {
        var message = sut.PasswordReset(Ana, AnaId, "a+b/c", Lifetime);

        message.Kind.Should().Be(EmailKind.PasswordReset);
        message.ToAddress.Should().Be("ana@test.com");
        message
            .TextBody.Should()
            .Contain($"https://app.test/reset-password#userId={AnaId}&code=a%2Bb%2Fc");
    }

    [Fact]
    public void ComingOfAgeNoticeLinksTheAccountPageAndGreetsTheGuardian()
    {
        var message = sut.ComingOfAgeNotice(Ana, "Leo Ruiz", new DateOnly(2026, 8, 3));

        message.Kind.Should().Be(EmailKind.ComingOfAgeNotice);
        message.ToAddress.Should().Be("ana@test.com");
        message.ToName.Should().Be("Ana");
        message
            .TextBody.Should()
            .Contain("https://app.test/account")
            .And.Contain("03/08/2026")
            .And.Contain(SiteFooter);
    }

    [Fact]
    public void EmailInUseValidRequestLinksTheLoginPageInsteadOfAnyCode()
    {
        var message = sut.EmailInUse(Ana);

        message.Kind.Should().Be(EmailKind.AccountVerification);
        message.ToAddress.Should().Be("ana@test.com");
        message.Subject.Should().Be(AppStrings.EmailsEmailInUseSubject);
        message
            .TextBody.Should()
            .Contain("https://app.test/login")
            .And.NotContain("verify-account")
            .And.NotContain("confirm-email");
    }

    [Fact]
    public void LoginCodeValidRequestShowsTheCodeAndItsLifetimeOutsideTheSubject()
    {
        var message = sut.LoginCode(Ana, "482913", Lifetime);

        message.Kind.Should().Be(EmailKind.TwoFactorCode);
        message.ToAddress.Should().Be("ana@test.com");
        message.ToName.Should().Be("Ana");
        message.Subject.Should().NotContain("482913");
        message
            .TextBody.Should()
            .Contain("\n482913\n")
            .And.Contain("15 minutos")
            .And.Contain(SiteFooter);
    }

    [Fact]
    public void AccountDeletionCodeValidRequestShowsTheCodeAndItsLifetimeOutsideTheSubject()
    {
        var message = sut.AccountDeletionCode(Ana, "482913", Lifetime);

        message.Kind.Should().Be(EmailKind.TwoFactorCode);
        message.ToAddress.Should().Be("ana@test.com");
        message.ToName.Should().Be("Ana");
        message.Subject.Should().Contain("eliminación").And.NotContain("482913");
        message
            .TextBody.Should()
            .Contain("\n482913\n")
            .And.Contain("15 minutos")
            .And.Contain(SiteFooter);
    }

    [Fact]
    public void BlockedDataConsultationCodeValidRequestShowsTheCodeAndItsLifetimeOutsideTheSubject()
    {
        var message = sut.BlockedDataConsultationCode(Ana, "482913", Lifetime);

        message.Kind.Should().Be(EmailKind.TwoFactorCode);
        message.ToAddress.Should().Be("ana@test.com");
        message.ToName.Should().Be("Ana");
        message.Subject.Should().Contain("datos bloqueados").And.NotContain("482913");
        message
            .TextBody.Should()
            .Contain("\n482913\n")
            .And.Contain("15 minutos")
            .And.Contain(SiteFooter);
    }

    [Fact]
    public void SecurityAlertMaskedNewEmailIsQuotedAndTheRecipientGreeted()
    {
        var message = sut.SecurityAlert(
            Ana,
            AccountSecurityChange.EmailChanged,
            clock.UtcNow,
            "b***@test.com"
        );

        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be("ana@test.com");
        message
            .TextBody.Should()
            .Contain("Ana")
            .And.Contain(AppStrings.EmailsSecurityAlertEmailChanged)
            .And.Contain("b***@test.com");
    }
}
