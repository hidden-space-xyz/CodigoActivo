using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email content for the one-time code that confirms opening a consultation of the
/// blocked data.
/// </summary>
public static class BlockedDataConsultationCodeEmail
{
    /// <summary>
    /// Creates a blocked data consultation code email from the validated request.
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
                AppStrings.EmailsBlockedDataConsultationCodeHeading,
                AppStrings.EmailsBlockedDataConsultationCodeIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsBlockedDataConsultationCodeIntroHtml,
                    AppStrings.EmailsBlockedDataConsultationCodeIntroText
                ),
                EmailBlocks.Code(code),
                EmailBlocks.Prose(
                    AppStrings.EmailsBlockedDataConsultationCodeExpiryHtml(minutes),
                    AppStrings.EmailsBlockedDataConsultationCodeExpiryText(minutes)
                ),
                EmailBlocks.Signoff(),
                EmailBlocks.Note(AppStrings.EmailsBlockedDataConsultationCodeIgnoreNote),
            ]
        );

        return content.ToMessage(
            EmailKind.TwoFactorCode,
            toAddress,
            toName,
            AppStrings.EmailsBlockedDataConsultationCodeSubject
        );
    }
}
