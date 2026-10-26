using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Infrastructure.Communication.Templates;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class SecurityAlertEmailTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 19, 10, 30, 0, TimeSpan.Zero);

    public static TheoryData<AccountSecurityChange, string> Sentences()
    {
        return new()
        {
            {
                AccountSecurityChange.CredentialChanged,
                AppStrings.EmailsSecurityAlertPasswordChanged
            },
            { AccountSecurityChange.CredentialReset, AppStrings.EmailsSecurityAlertPasswordReset },
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
            { AccountSecurityChange.AddressChanged, AppStrings.EmailsSecurityAlertEmailChanged },
            { AccountSecurityChange.PhoneChanged, AppStrings.EmailsSecurityAlertPhoneChanged },
            {
                AccountSecurityChange.AddressAndPhoneChanged,
                AppStrings.EmailsSecurityAlertEmailAndPhoneChanged
            },
            {
                AccountSecurityChange.CredentialLocked,
                AppStrings.EmailsSecurityAlertPasswordLocked
            },
        };
    }

    public static TheoryData<AccountSecurityChange, string> Warnings()
    {
        return new()
        {
            { AccountSecurityChange.CredentialChanged, AppStrings.EmailsSecurityAlertWarning },
            { AccountSecurityChange.CredentialReset, AppStrings.EmailsSecurityAlertWarning },
            { AccountSecurityChange.AuthenticatorEnabled, AppStrings.EmailsSecurityAlertWarning },
            { AccountSecurityChange.AuthenticatorDisabled, AppStrings.EmailsSecurityAlertWarning },
            { AccountSecurityChange.PhoneChanged, AppStrings.EmailsSecurityAlertWarning },
            {
                AccountSecurityChange.TwoFactorReset,
                AppStrings.EmailsSecurityAlertUnexpectedWarning
            },
            { AccountSecurityChange.AdminGranted, AppStrings.EmailsSecurityAlertUnexpectedWarning },
            { AccountSecurityChange.AdminRevoked, AppStrings.EmailsSecurityAlertUnexpectedWarning },
            {
                AccountSecurityChange.AddressChanged,
                AppStrings.EmailsSecurityAlertEmailChangedWarning
            },
            {
                AccountSecurityChange.AddressAndPhoneChanged,
                AppStrings.EmailsSecurityAlertEmailChangedWarning
            },
            {
                AccountSecurityChange.CredentialLocked,
                AppStrings.EmailsSecurityAlertPasswordLockedWarning
            },
        };
    }

    [Theory]
    [MemberData(nameof(Warnings))]
    public void CreateGivesTheAdviceThatFitsEveryChange(
        AccountSecurityChange change,
        string expected
    )
    {
        var message = SecurityAlertEmail.Create(
            "owner@test.com",
            "Owner",
            change,
            OccurredAt,
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message.TextBody.Should().Contain(expected);
        var others = new[]
        {
            AppStrings.EmailsSecurityAlertWarning,
            AppStrings.EmailsSecurityAlertUnexpectedWarning,
            AppStrings.EmailsSecurityAlertEmailChangedWarning,
            AppStrings.EmailsSecurityAlertPasswordLockedWarning,
        }.Where(warning => warning != expected);
        foreach (var other in others)
        {
            message.TextBody.Should().NotContain(other);
        }
    }

    [Theory]
    [MemberData(nameof(Sentences))]
    public void CreateRendersTheSentenceForEveryChange(
        AccountSecurityChange change,
        string expected
    )
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
        message.TextBody.Should().Contain(expected);
        message.HtmlBody.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(Sentences))]
    public void CreateTitlesAPasswordLockApartFromTheOtherChanges(
        AccountSecurityChange change,
        string sentence
    )
    {
        var message = SecurityAlertEmail.Create(
            "owner@test.com",
            "Owner",
            change,
            OccurredAt,
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        var locked = change is AccountSecurityChange.CredentialLocked;
        message
            .Subject.Should()
            .Be(
                locked
                    ? AppStrings.EmailsSecurityAlertPasswordLockedSubject
                    : AppStrings.EmailsSecurityAlertSubject,
                sentence
            );
        message
            .TextBody.Should()
            .Contain(
                locked
                    ? AppStrings.EmailsSecurityAlertPasswordLockedHeading
                    : AppStrings.EmailsSecurityAlertHeading
            );
    }

    [Fact]
    public void CreateHtmlEncodesAHostileRecipientName()
    {
        var message = SecurityAlertEmail.Create(
            "owner@test.com",
            "<script>alert(1)</script>",
            AccountSecurityChange.CredentialChanged,
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
            AccountSecurityChange.AddressChanged,
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
            AccountSecurityChange.AddressChanged,
            OccurredAt,
            TimeZoneInfo.Utc,
            "https://app.test"
        );

        message.TextBody.Should().NotContain("n***@test.com");
    }
}
