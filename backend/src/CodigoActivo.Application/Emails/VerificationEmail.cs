using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Builds the email content for verification.
/// </summary>
public static class VerificationEmail
{
    /// <summary>
    /// Creates a verification email from the validated request.
    /// </summary>
    /// <param name="toAddress">The to address value.</param>
    /// <param name="toName">The to name value.</param>
    /// <param name="verificationUrl">The verification url value.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <param name="lifetime">The lifetime value.</param>
    /// <returns>The resulting email message value.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string verificationUrl,
        string siteUrl,
        TimeSpan lifetime
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));

        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsVerificationHeading,
                AppStrings.EmailsVerificationIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand,
                AppStrings.EmailsFooterAutomaticNote
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsVerificationIntroHtml,
                    AppStrings.EmailsVerificationIntroText
                ),
                EmailBlocks.Action(AppStrings.EmailsVerificationButtonLabel, verificationUrl),
                EmailBlocks.Prose(
                    AppStrings.EmailsVerificationExpiryHtml(minutes),
                    AppStrings.EmailsVerificationExpiryText(minutes)
                ),
                EmailBlocks.Prose(
                    AppStrings.EmailsSharedSignoffHtml,
                    AppStrings.EmailsSharedSignoffText
                ),
                EmailBlocks.Note(AppStrings.EmailsVerificationIgnoreNote),
            ]
        );

        return new EmailMessage(
            EmailKind.AccountVerification,
            toAddress,
            toName,
            AppStrings.EmailsVerificationSubject,
            content.Html,
            content.Text,
            InlineImages: content.InlineImages
        );
    }
}
