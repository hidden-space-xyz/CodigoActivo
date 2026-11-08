using System.Globalization;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email content that tells an account owner of a security change to their account:
/// password, second factor, staff role, data protection designation, email or phone, or a lock
/// after wrong passwords or wrong second-factor codes. The message carries no code, no secret and
/// no link that performs an action.
/// </summary>
public static class SecurityAlertEmail
{
    private const string TimestampFormat = "dd/MM/yyyy, HH:mm";

    /// <summary>
    /// Creates the alert: what changed, when, the masked new address when given, and the advice
    /// that fits the change. Each lock has a subject and heading of its own.
    /// </summary>
    /// <param name="toAddress">
    /// Address of the account owner; the previous one when the email or phone changed.
    /// </param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="change">Change being notified.</param>
    /// <param name="occurredAt">When the change happened.</param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <param name="maskedNewEmail">
    /// Masked new address, when the email changed; <see langword="null"/> or blank leaves it out.
    /// </param>
    /// <returns>The message, of kind <see cref="EmailKind.SecurityAlert"/>.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        AccountSecurityChange change,
        DateTimeOffset occurredAt,
        TimeZoneInfo timeZone,
        string siteUrl,
        string? maskedNewEmail = null
    )
    {
        var blocks = new List<EmailBlock>
        {
            EmailBlocks.Prose(
                AppStrings.EmailsSecurityAlertIntroHtml,
                AppStrings.EmailsSecurityAlertIntroText
            ),
            EmailBlocks.Callout(Sentence(change), EmailBranding.Brand),
            EmailBlocks.Prose(AppStrings.EmailsSecurityAlertWhen(Timestamp(occurredAt, timeZone))),
        };

        if (!string.IsNullOrWhiteSpace(maskedNewEmail))
        {
            blocks.Add(EmailBlocks.Prose(AppStrings.EmailsSecurityAlertNewEmail(maskedNewEmail)));
        }

        blocks.Add(EmailBlocks.Prose(Warning(change)));
        blocks.Add(EmailBlocks.Signoff());

        var content = EmailLayout.Render(
            new EmailDocument(
                Heading(change),
                AppStrings.EmailsSecurityAlertIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            blocks
        );

        return content.ToMessage(EmailKind.SecurityAlert, toAddress, toName, Subject(change));
    }

    private static string Heading(AccountSecurityChange change)
    {
        return change switch
        {
            AccountSecurityChange.CredentialLocked =>
                AppStrings.EmailsSecurityAlertPasswordLockedHeading,
            AccountSecurityChange.TwoFactorLocked =>
                AppStrings.EmailsSecurityAlertTwoFactorLockedHeading,
            _ => AppStrings.EmailsSecurityAlertHeading,
        };
    }

    private static string Subject(AccountSecurityChange change)
    {
        return change switch
        {
            AccountSecurityChange.CredentialLocked =>
                AppStrings.EmailsSecurityAlertPasswordLockedSubject,
            AccountSecurityChange.TwoFactorLocked =>
                AppStrings.EmailsSecurityAlertTwoFactorLockedSubject,
            _ => AppStrings.EmailsSecurityAlertSubject,
        };
    }

    private static string Timestamp(DateTimeOffset occurredAt, TimeZoneInfo timeZone)
    {
        return TimeZoneInfo
            .ConvertTime(occurredAt, timeZone)
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }

    private static string Sentence(AccountSecurityChange change)
    {
        return change switch
        {
            AccountSecurityChange.CredentialChanged =>
                AppStrings.EmailsSecurityAlertPasswordChanged,
            AccountSecurityChange.CredentialReset => AppStrings.EmailsSecurityAlertPasswordReset,
            AccountSecurityChange.AuthenticatorEnabled =>
                AppStrings.EmailsSecurityAlertAuthenticatorEnabled,
            AccountSecurityChange.AuthenticatorDisabled =>
                AppStrings.EmailsSecurityAlertAuthenticatorDisabled,
            AccountSecurityChange.TwoFactorReset => AppStrings.EmailsSecurityAlertTwoFactorReset,
            AccountSecurityChange.EditorGranted => AppStrings.EmailsSecurityAlertEditorGranted,
            AccountSecurityChange.AdministratorGranted =>
                AppStrings.EmailsSecurityAlertAdministratorGranted,
            AccountSecurityChange.StaffRoleRevoked =>
                AppStrings.EmailsSecurityAlertStaffRoleRevoked,
            AccountSecurityChange.PrivacyOfficerDesignated =>
                AppStrings.EmailsSecurityAlertPrivacyOfficerDesignated,
            AccountSecurityChange.PrivacyOfficerReleased =>
                AppStrings.EmailsSecurityAlertPrivacyOfficerReleased,
            AccountSecurityChange.AddressChanged => AppStrings.EmailsSecurityAlertEmailChanged,
            AccountSecurityChange.PhoneChanged => AppStrings.EmailsSecurityAlertPhoneChanged,
            AccountSecurityChange.AddressAndPhoneChanged =>
                AppStrings.EmailsSecurityAlertEmailAndPhoneChanged,
            AccountSecurityChange.CredentialLocked => AppStrings.EmailsSecurityAlertPasswordLocked,
            AccountSecurityChange.TwoFactorLocked => AppStrings.EmailsSecurityAlertTwoFactorLocked,
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null),
        };
    }

    private static string Warning(AccountSecurityChange change)
    {
        return change switch
        {
            AccountSecurityChange.AddressChanged or AccountSecurityChange.AddressAndPhoneChanged =>
                AppStrings.EmailsSecurityAlertEmailChangedWarning,
            AccountSecurityChange.TwoFactorReset
            or AccountSecurityChange.EditorGranted
            or AccountSecurityChange.AdministratorGranted
            or AccountSecurityChange.StaffRoleRevoked
            or AccountSecurityChange.PrivacyOfficerDesignated
            or AccountSecurityChange.PrivacyOfficerReleased =>
                AppStrings.EmailsSecurityAlertUnexpectedWarning,
            AccountSecurityChange.CredentialLocked =>
                AppStrings.EmailsSecurityAlertPasswordLockedWarning,
            AccountSecurityChange.TwoFactorLocked =>
                AppStrings.EmailsSecurityAlertTwoFactorLockedWarning,
            _ => AppStrings.EmailsSecurityAlertWarning,
        };
    }
}
