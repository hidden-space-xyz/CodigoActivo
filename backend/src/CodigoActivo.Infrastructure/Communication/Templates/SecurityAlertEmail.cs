using System.Globalization;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email content that tells an account owner of a security change to their account: password,
/// second factor, administrator rights, email or phone, or a lock after wrong passwords. The message
/// carries no code, no secret and no link that performs an action.
/// </summary>
public static class SecurityAlertEmail
{
    private const string TimestampFormat = "dd/MM/yyyy, HH:mm";

    /// <summary>
    /// Creates the alert: what changed, when, the masked new address when given, and the advice that fits
    /// the change. A lock has a subject and heading of its own.
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
    /// Masked new address, when the email changed; null or blank leaves it out.
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

        var locked = change is AccountSecurityChange.CredentialLocked;
        var content = EmailLayout.Render(
            new EmailDocument(
                locked
                    ? AppStrings.EmailsSecurityAlertPasswordLockedHeading
                    : AppStrings.EmailsSecurityAlertHeading,
                AppStrings.EmailsSecurityAlertIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            blocks
        );

        return content.ToMessage(
            EmailKind.SecurityAlert,
            toAddress,
            toName,
            locked
                ? AppStrings.EmailsSecurityAlertPasswordLockedSubject
                : AppStrings.EmailsSecurityAlertSubject
        );
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
            AccountSecurityChange.AdminGranted => AppStrings.EmailsSecurityAlertAdminGranted,
            AccountSecurityChange.AdminRevoked => AppStrings.EmailsSecurityAlertAdminRevoked,
            AccountSecurityChange.AddressChanged => AppStrings.EmailsSecurityAlertEmailChanged,
            AccountSecurityChange.PhoneChanged => AppStrings.EmailsSecurityAlertPhoneChanged,
            AccountSecurityChange.AddressAndPhoneChanged =>
                AppStrings.EmailsSecurityAlertEmailAndPhoneChanged,
            AccountSecurityChange.CredentialLocked => AppStrings.EmailsSecurityAlertPasswordLocked,
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
            or AccountSecurityChange.AdminGranted
            or AccountSecurityChange.AdminRevoked =>
                AppStrings.EmailsSecurityAlertUnexpectedWarning,
            AccountSecurityChange.CredentialLocked =>
                AppStrings.EmailsSecurityAlertPasswordLockedWarning,
            _ => AppStrings.EmailsSecurityAlertWarning,
        };
    }
}
