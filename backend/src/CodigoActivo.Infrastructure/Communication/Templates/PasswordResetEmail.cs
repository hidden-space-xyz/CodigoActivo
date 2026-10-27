using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email with the link that lets an account holder choose a new password.
/// </summary>
public static class PasswordResetEmail
{
    /// <summary>
    /// Creates the email with the reset button, the minutes the link lasts and a note for whoever did not
    /// ask for it.
    /// </summary>
    /// <param name="toAddress">Address of the account holder.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="resetUrl">Link to the page that sets the new password.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <param name="lifetime">Validity of the link, shown in whole minutes rounded up, at least one.</param>
    /// <returns>The message, of kind <see cref="EmailKind.AccessRecovery"/>.</returns>
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
                EmailBranding.Brand
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
                EmailBlocks.Signoff(),
                EmailBlocks.Note(AppStrings.EmailsPasswordResetIgnoreNote),
            ]
        );

        return content.ToMessage(
            EmailKind.AccessRecovery,
            toAddress,
            toName,
            AppStrings.EmailsPasswordResetSubject
        );
    }
}
