using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email content for the one-time code that confirms deleting an account.
/// </summary>
public static class AccountDeletionCodeEmail
{
    /// <summary>
    /// Creates the email with the code, the minutes it lasts and a warning for whoever did not ask to
    /// delete the account.
    /// </summary>
    /// <param name="toAddress">Address of the account holder.</param>
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
                AppStrings.EmailsAccountDeletionCodeHeading,
                AppStrings.EmailsAccountDeletionCodeIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand
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
                EmailBlocks.Signoff(),
                EmailBlocks.Note(AppStrings.EmailsAccountDeletionCodeIgnoreNote),
            ]
        );

        return content.ToMessage(
            EmailKind.TwoFactorCode,
            toAddress,
            toName,
            AppStrings.EmailsAccountDeletionCodeSubject
        );
    }
}
