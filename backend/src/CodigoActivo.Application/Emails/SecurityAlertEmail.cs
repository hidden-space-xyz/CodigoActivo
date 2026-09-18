using System.Globalization;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Identifies the change to the authentication data of an account that is worth notifying.
/// </summary>
public enum AccountSecurityChange
{
    /// <summary>
    /// Selects the password changed option.
    /// </summary>
    PasswordChanged,
    /// <summary>
    /// Selects the password reset through recovery option.
    /// </summary>
    PasswordReset,
    /// <summary>
    /// Selects the authenticator confirmed option.
    /// </summary>
    AuthenticatorEnabled,
    /// <summary>
    /// Selects the authenticator removed option.
    /// </summary>
    AuthenticatorDisabled,
    /// <summary>
    /// Selects the second factor reset by an administrator option.
    /// </summary>
    TwoFactorReset,
    /// <summary>
    /// Selects the administrator flag granted option.
    /// </summary>
    AdminGranted,
    /// <summary>
    /// Selects the administrator flag revoked option.
    /// </summary>
    AdminRevoked,
    /// <summary>
    /// Selects the login identifiers replaced option.
    /// </summary>
    IdentifiersChanged,
}

/// <summary>
/// Builds the email content that tells an account owner their authentication data changed. The
/// message carries no code, no secret and no link that performs an action.
/// </summary>
public static class SecurityAlertEmail
{
    private const string TimestampFormat = "dd/MM/yyyy HH:mm";

    /// <summary>
    /// Creates a security change notification from the affected account data.
    /// </summary>
    /// <param name="toAddress">The to address value.</param>
    /// <param name="toName">The to name value.</param>
    /// <param name="change">The change value.</param>
    /// <param name="occurredAt">The occurred at value.</param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <param name="maskedNewEmail">Masked new address, when the login identifiers changed.</param>
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

        blocks.Add(EmailBlocks.Prose(AppStrings.EmailsSecurityAlertWarning));
        blocks.Add(
            EmailBlocks.Prose(AppStrings.EmailsSharedSignoffHtml, AppStrings.EmailsSharedSignoffText)
        );
        blocks.Add(EmailBlocks.Note(AppStrings.EmailsSecurityAlertNoActionNote));

        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsSecurityAlertHeading,
                AppStrings.EmailsSecurityAlertIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand,
                AppStrings.EmailsFooterAutomaticNote
            ),
            blocks
        );

        return new EmailMessage(
            EmailKind.SecurityAlert,
            toAddress,
            toName,
            AppStrings.EmailsSecurityAlertSubject,
            content.Html,
            content.Text,
            InlineImages: content.InlineImages
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
            AccountSecurityChange.PasswordChanged => AppStrings.EmailsSecurityAlertPasswordChanged,
            AccountSecurityChange.PasswordReset => AppStrings.EmailsSecurityAlertPasswordReset,
            AccountSecurityChange.AuthenticatorEnabled =>
                AppStrings.EmailsSecurityAlertAuthenticatorEnabled,
            AccountSecurityChange.AuthenticatorDisabled =>
                AppStrings.EmailsSecurityAlertAuthenticatorDisabled,
            AccountSecurityChange.TwoFactorReset => AppStrings.EmailsSecurityAlertTwoFactorReset,
            AccountSecurityChange.AdminGranted => AppStrings.EmailsSecurityAlertAdminGranted,
            AccountSecurityChange.AdminRevoked => AppStrings.EmailsSecurityAlertAdminRevoked,
            AccountSecurityChange.IdentifiersChanged =>
                AppStrings.EmailsSecurityAlertIdentifiersChanged,
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null),
        };
    }
}
