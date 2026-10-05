using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the notice that tells the holder of an address that someone tried to use it for another
/// account. It is sent instead of the link that would verify the address, so it is budgeted as one.
/// </summary>
public static class EmailInUseEmail
{
    /// <summary>
    /// Creates the notice for the holder of the address.
    /// </summary>
    /// <param name="toAddress">Address that already has an account.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="loginUrl">Login page of the site.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <returns>The resulting email message value.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string loginUrl,
        string siteUrl
    )
    {
        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsEmailInUseHeading,
                AppStrings.EmailsEmailInUseIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand,
                AppStrings.EmailsFooterAutomaticNote
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsEmailInUseIntroHtml,
                    AppStrings.EmailsEmailInUseIntroText
                ),
                EmailBlocks.Action(AppStrings.EmailsEmailInUseButtonLabel, loginUrl),
                EmailBlocks.Prose(AppStrings.EmailsEmailInUseRecovery),
                EmailBlocks.Prose(
                    AppStrings.EmailsSharedSignoffHtml,
                    AppStrings.EmailsSharedSignoffText
                ),
                EmailBlocks.Note(AppStrings.EmailsEmailInUseIgnoreNote),
            ]
        );

        return new EmailMessage(
            EmailKind.AccountVerification,
            toAddress,
            toName,
            AppStrings.EmailsEmailInUseSubject,
            content.Html,
            content.Text,
            InlineImages: content.InlineImages
        );
    }
}
