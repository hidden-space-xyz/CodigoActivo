using System.Globalization;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email content that tells an account owner their authentication data changed. The
/// message carries no code, no secret and no link that performs an action.
/// </summary>
public static class SecurityAlertEmail
{
    private const string TimestampFormat = "dd/MM/yyyy, HH:mm";

    /// <summary>
    /// Creates a security change notification from the affected account data.
    /// </summary>
    /// <param name="toAddress">The to address value.</param>
    /// <param name="toName">The to name value.</param>
    /// <param name="change">The change value.</param>
    /// <param name="occurredAt">The occurred at value.</param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <param name="maskedNewEmail">Masked new address, when the email changed.</param>
    /// <returns>The resulting email message value.</returns>
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
