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
    /// Creates the email with the code, the minutes it lasts and a warning for whoever did not ask to
    /// open the consultation.
    /// </summary>
    /// <param name="toAddress">Address of the account designated for data protection.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="code">Plain code the user must type.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <param name="lifetime">Validity of the code, shown in whole minutes rounded up, at least one.</param>
    /// <returns>The message, of kind <see cref="EmailKind.TwoFactorCode"/>.</returns>
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
