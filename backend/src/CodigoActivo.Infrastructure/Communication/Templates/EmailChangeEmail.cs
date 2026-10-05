using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email content that confirms the new email an account holder asked for.
/// </summary>
public static class EmailChangeEmail
{
    /// <summary>
    /// Creates the confirmation email sent to the new address.
    /// </summary>
    /// <param name="toAddress">New address receiving the message.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="confirmationUrl">Link that confirms the new address.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <param name="lifetime">Validity of the link.</param>
    /// <returns>The resulting email message value.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string confirmationUrl,
        string siteUrl,
        TimeSpan lifetime
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));

        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsEmailChangeHeading,
                AppStrings.EmailsEmailChangeIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand,
                AppStrings.EmailsFooterAutomaticNote
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsEmailChangeIntroHtml,
                    AppStrings.EmailsEmailChangeIntroText
                ),
                EmailBlocks.Action(AppStrings.EmailsEmailChangeButtonLabel, confirmationUrl),
                EmailBlocks.Prose(
                    AppStrings.EmailsEmailChangeExpiryHtml(minutes),
                    AppStrings.EmailsEmailChangeExpiryText(minutes)
                ),
                EmailBlocks.Prose(
                    AppStrings.EmailsSharedSignoffHtml,
                    AppStrings.EmailsSharedSignoffText
                ),
                EmailBlocks.Note(AppStrings.EmailsEmailChangeIgnoreNote),
            ]
        );

        return new EmailMessage(
            EmailKind.AccountVerification,
            toAddress,
            toName,
            AppStrings.EmailsEmailChangeSubject,
            content.Html,
            content.Text,
            InlineImages: content.InlineImages
        );
    }
}
