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
    /// Creates the email with the verification button, the minutes the link lasts, the news consent when
    /// given and a note for whoever did not register.
    /// </summary>
    /// <param name="toAddress">Address of the new account.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="verificationUrl">Link to the page that verifies the account.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <param name="lifetime">Validity of the link, shown in whole minutes rounded up, at least one.</param>
    /// <param name="newsConsent">Whether the registration accepted news by email.</param>
    /// <returns>The message, of kind <see cref="EmailKind.AccountVerification"/>.</returns>
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
                EmailBlocks.Signoff(),
                EmailBlocks.Note(AppStrings.EmailsVerificationIgnoreNote),
            ]
        );

        return content.ToMessage(
            EmailKind.AccountVerification,
            toAddress,
            toName,
            AppStrings.EmailsVerificationSubject
        );
    }
}
