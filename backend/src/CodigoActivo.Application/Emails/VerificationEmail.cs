using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public static class VerificationEmail
{
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string code,
        string verificationUrl,
        string siteUrl,
        TimeSpan lifetime
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));

        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsVerificationHeading,
                AppStrings.EmailsVerificationIntroText,
                toName,
                siteUrl,
                EmailBranding.Brand,
                AppStrings.EmailsFooterAutomaticNote
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsVerificationIntroHtml,
                    AppStrings.EmailsVerificationIntroText
                ),
                EmailBlocks.Action(AppStrings.EmailsVerificationButtonLabel, verificationUrl),
                EmailBlocks.Code(AppStrings.EmailsVerificationCodePrompt, code),
                EmailBlocks.Prose(
                    AppStrings.EmailsVerificationExpiryHtml(minutes),
                    AppStrings.EmailsVerificationExpiryText(minutes)
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
