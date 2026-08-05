using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public static class PasswordResetEmail
{
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
                EmailBranding.Brand,
                AppStrings.EmailsFooterAutomaticNote
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
                EmailBlocks.Prose(
                    AppStrings.EmailsSharedSignoffHtml,
                    AppStrings.EmailsSharedSignoffText
                ),
                EmailBlocks.Note(AppStrings.EmailsPasswordResetIgnoreNote),
            ]
        );

        return new EmailMessage(
            EmailKind.PasswordReset,
            toAddress,
            toName,
            AppStrings.EmailsPasswordResetSubject,
            content.Html,
            content.Text,
            InlineImages: content.InlineImages
        );
    }
}
