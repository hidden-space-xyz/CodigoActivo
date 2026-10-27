using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email content for the one-time code that completes a login.
/// </summary>
public static class LoginCodeEmail
{
    /// <summary>
    /// Creates the email with the code, the minutes it lasts and a warning for whoever did not try to log
    /// in.
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
                AppStrings.EmailsLoginCodeHeading,
                AppStrings.EmailsLoginCodeIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsLoginCodeIntroHtml,
                    AppStrings.EmailsLoginCodeIntroText
                ),
                EmailBlocks.Code(code),
                EmailBlocks.Prose(
                    AppStrings.EmailsLoginCodeExpiryHtml(minutes),
                    AppStrings.EmailsLoginCodeExpiryText(minutes)
                ),
                EmailBlocks.Signoff(),
                EmailBlocks.Note(AppStrings.EmailsLoginCodeIgnoreNote),
            ]
        );

        return content.ToMessage(
            EmailKind.TwoFactorCode,
            toAddress,
            toName,
            AppStrings.EmailsLoginCodeSubject
        );
    }
}
