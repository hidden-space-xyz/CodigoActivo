using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email content for verification. A registration that accepted news says so, so the
/// owner of the address learns of it even if someone else registered it.
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
    /// <param name="newsConsent">Whether the registration accepted news by email.</param>
    /// <returns>The resulting email message value.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string verificationUrl,
        string siteUrl,
        TimeSpan lifetime,
        bool newsConsent
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));

        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsVerificationHeading,
                AppStrings.EmailsVerificationIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand
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
                .. (
                    newsConsent
                        ? [EmailBlocks.Prose(AppStrings.EmailsVerificationNewsConsent)]
                        : Array.Empty<EmailBlock>()
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
