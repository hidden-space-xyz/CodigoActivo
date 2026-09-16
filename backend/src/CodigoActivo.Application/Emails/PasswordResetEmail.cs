using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Builds the email content for password reset.
/// </summary>
public static class PasswordResetEmail
{
    /// <summary>
    /// Creates a password reset email from the validated request.
    /// </summary>
    /// <param name="toAddress">The to address value.</param>
    /// <param name="toName">The to name value.</param>
    /// <param name="resetUrl">The reset url value.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <param name="lifetime">The lifetime value.</param>
    /// <returns>The resulting email message value.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string resetUrl,
        string siteUrl,
        TimeSpan lifetime
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));

        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsPasswordResetHeading,
                AppStrings.EmailsPasswordResetIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand,
                AppStrings.EmailsFooterAutomaticNote
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsPasswordResetIntroHtml,
                    AppStrings.EmailsPasswordResetIntroText
                ),
                EmailBlocks.Action(AppStrings.EmailsPasswordResetButtonLabel, resetUrl),
                EmailBlocks.Prose(
                    AppStrings.EmailsPasswordResetExpiryHtml(minutes),
                    AppStrings.EmailsPasswordResetExpiryText(minutes)
                ),
                EmailBlocks.Prose(
                    AppStrings.EmailsSharedSignoffHtml,
                    AppStrings.EmailsSharedSignoffText
                ),
                EmailBlocks.Note(AppStrings.EmailsPasswordResetIgnoreNote),
            ]
        );

        return new EmailMessage(
            EmailKind.PasswordReset,
            toAddress,
            toName,
            AppStrings.EmailsPasswordResetSubject,
            content.Html,
            content.Text,
            InlineImages: content.InlineImages
        );
    }
}
