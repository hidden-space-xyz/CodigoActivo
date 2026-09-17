using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Builds the email content for the one-time code that confirms deleting an account.
/// </summary>
public static class AccountDeletionCodeEmail
{
    /// <summary>
    /// Creates an account deletion code email from the validated request.
    /// </summary>
    /// <param name="toAddress">The to address value.</param>
    /// <param name="toName">The to name value.</param>
    /// <param name="code">Plain code the user must type.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <param name="lifetime">The lifetime value.</param>
    /// <returns>The resulting email message value.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string code,
        string siteUrl,
        TimeSpan lifetime
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));

        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsAccountDeletionCodeHeading,
                AppStrings.EmailsAccountDeletionCodeIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand,
                AppStrings.EmailsFooterAutomaticNote
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsAccountDeletionCodeIntroHtml,
                    AppStrings.EmailsAccountDeletionCodeIntroText
                ),
                EmailBlocks.Code(code),
                EmailBlocks.Prose(
                    AppStrings.EmailsAccountDeletionCodeExpiryHtml(minutes),
                    AppStrings.EmailsAccountDeletionCodeExpiryText(minutes)
                ),
                EmailBlocks.Prose(
                    AppStrings.EmailsSharedSignoffHtml,
                    AppStrings.EmailsSharedSignoffText
                ),
                EmailBlocks.Note(AppStrings.EmailsAccountDeletionCodeIgnoreNote),
            ]
        );

        return new EmailMessage(
            EmailKind.TwoFactorCode,
            toAddress,
            toName,
            AppStrings.EmailsAccountDeletionCodeSubject,
            content.Html,
            content.Text,
            InlineImages: content.InlineImages
        );
    }
}
