using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails;

public sealed class SecurityAlertEmailTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 19, 10, 30, 0, TimeSpan.Zero);

    public static TheoryData<AccountSecurityChange, string> Sentences()
    {
        return new()
        {
            { AccountSecurityChange.PasswordChanged, AppStrings.EmailsSecurityAlertPasswordChanged },
            { AccountSecurityChange.PasswordReset, AppStrings.EmailsSecurityAlertPasswordReset },
            {
                AccountSecurityChange.AuthenticatorEnabled,
                AppStrings.EmailsSecurityAlertAuthenticatorEnabled
            },
            {
                AccountSecurityChange.AuthenticatorDisabled,
                AppStrings.EmailsSecurityAlertAuthenticatorDisabled
            },
            { AccountSecurityChange.TwoFactorReset, AppStrings.EmailsSecurityAlertTwoFactorReset },
            { AccountSecurityChange.AdminGranted, AppStrings.EmailsSecurityAlertAdminGranted },
            { AccountSecurityChange.AdminRevoked, AppStrings.EmailsSecurityAlertAdminRevoked },
            {
                AccountSecurityChange.IdentifiersChanged,
                AppStrings.EmailsSecurityAlertIdentifiersChanged
            },
            { AccountSecurityChange.PasswordLocked, AppStrings.EmailsSecurityAlertPasswordLocked },
        };
    }

    [Theory]
    [MemberData(nameof(Sentences))]
    public void CreateRendersTheSentenceForEveryChange(AccountSecurityChange change, string expected)
    {
        var message = SecurityAlertEmail.Create(
            "owner@test.com",
            "Owner",
            change,
            OccurredAt,
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.Subject.Should().Be(AppStrings.EmailsSecurityAlertSubject);
        message.TextBody.Should().Contain(expected);
        message.HtmlBody.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreateHtmlEncodesAHostileRecipientName()
    {
        var message = SecurityAlertEmail.Create(
            "owner@test.com",
            "<script>alert(1)</script>",
            AccountSecurityChange.PasswordChanged,
            OccurredAt,
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message
            .HtmlBody.Should()
            .NotContain("<script>alert(1)</script>")
            .And.Contain("&lt;script&gt;");
    }

    [Fact]
    public void CreateNeverIncludesACodeOrToken()
    {
        var message = SecurityAlertEmail.Create(
            "owner@test.com",
            "Owner",
            AccountSecurityChange.IdentifiersChanged,
            OccurredAt,
            TimeZoneInfo.Utc,
            "https://app.test",
            maskedNewEmail: "n***@test.com"
        );

        message.TextBody.Should().NotContain("otp").And.NotContain("token").And.NotContain("code=");
        message.HtmlBody.Should().NotContain("otp").And.NotContain("token").And.NotContain("code=");
    }

    [Fact]
    public void CreateWithoutAMaskedNewEmailOmitsTheNewEmailLine()
    {
        var message = SecurityAlertEmail.Create(
            "owner@test.com",
            "Owner",
            AccountSecurityChange.IdentifiersChanged,
            OccurredAt,
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message.TextBody.Should().NotContain("n***@test.com");
    }
}
