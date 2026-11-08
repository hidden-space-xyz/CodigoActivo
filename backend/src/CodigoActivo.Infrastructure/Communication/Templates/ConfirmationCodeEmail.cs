using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email content for the one-time code that confirms a change of an account: deleting
/// it, moving it to another email or enrolling an authenticator application.
/// </summary>
public static class ConfirmationCodeEmail
{
    /// <summary>
    /// Creates the email with the code, the change it confirms, the minutes it lasts and a warning
    /// for whoever did not ask for that change.
    /// </summary>
    /// <param name="toAddress">Address of the account holder.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="purpose">Change the code confirms.</param>
    /// <param name="code">Plain code the user must type.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <param name="lifetime">
    /// Validity of the code, shown in whole minutes rounded up, at least one.
    /// </param>
    /// <returns>The message, of kind <see cref="EmailKind.TwoFactorCode"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The purpose is not a known one.</exception>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        ConfirmationCodePurpose purpose,
        string code,
        string siteUrl,
        TimeSpan lifetime
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));
        var texts = TextsOf(purpose, minutes);

        var content = EmailLayout.Render(
            new EmailDocument(texts.Heading, texts.IntroText, toName, siteUrl, EmailBranding.Brand),
            [
                EmailBlocks.Prose(texts.IntroHtml, texts.IntroText),
                EmailBlocks.Code(code),
                EmailBlocks.Prose(texts.ExpiryHtml, texts.ExpiryText),
                EmailBlocks.Signoff(),
                EmailBlocks.Note(texts.IgnoreNote),
            ]
        );

        return content.ToMessage(EmailKind.TwoFactorCode, toAddress, toName, texts.Subject);
    }

    private static CodeTexts TextsOf(ConfirmationCodePurpose purpose, int minutes)
    {
        return purpose switch
        {
            ConfirmationCodePurpose.AccountDeletion => new CodeTexts(
                AppStrings.EmailsAccountDeletionCodeSubject,
                AppStrings.EmailsAccountDeletionCodeHeading,
                AppStrings.EmailsAccountDeletionCodeIntroText,
                AppStrings.EmailsAccountDeletionCodeIntroHtml,
                AppStrings.EmailsAccountDeletionCodeExpiryText(minutes),
                AppStrings.EmailsAccountDeletionCodeExpiryHtml(minutes),
                AppStrings.EmailsAccountDeletionCodeIgnoreNote
            ),
            ConfirmationCodePurpose.EmailChange => new CodeTexts(
                AppStrings.EmailsEmailChangeCodeSubject,
                AppStrings.EmailsEmailChangeCodeHeading,
                AppStrings.EmailsEmailChangeCodeIntroText,
                AppStrings.EmailsEmailChangeCodeIntroHtml,
                AppStrings.EmailsEmailChangeCodeExpiryText(minutes),
                AppStrings.EmailsEmailChangeCodeExpiryHtml(minutes),
                AppStrings.EmailsEmailChangeCodeIgnoreNote
            ),
            ConfirmationCodePurpose.AuthenticatorSetup => new CodeTexts(
                AppStrings.EmailsAuthenticatorSetupCodeSubject,
                AppStrings.EmailsAuthenticatorSetupCodeHeading,
                AppStrings.EmailsAuthenticatorSetupCodeIntroText,
                AppStrings.EmailsAuthenticatorSetupCodeIntroHtml,
                AppStrings.EmailsAuthenticatorSetupCodeExpiryText(minutes),
                AppStrings.EmailsAuthenticatorSetupCodeExpiryHtml(minutes),
                AppStrings.EmailsAuthenticatorSetupCodeIgnoreNote
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(purpose),
                purpose,
                "The confirmation code purpose is not a known one."
            ),
        };
    }

    private sealed record CodeTexts(
        string Subject,
        string Heading,
        string IntroText,
        string IntroHtml,
        string ExpiryText,
        string ExpiryHtml,
        string IgnoreNote
    );
}
