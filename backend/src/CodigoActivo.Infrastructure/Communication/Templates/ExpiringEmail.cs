using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the emails that carry a one-time code or link: an introduction, the code or the button
/// that opens the link, the minutes it lasts rounded up and at least one, the sign-off and a note
/// for whoever did not ask for it.
/// </summary>
internal static class ExpiringEmail
{
    /// <summary>
    /// Gets the texts of the link that verifies a new account.
    /// </summary>
    internal static Texts Verification =>
        new(
            AppStrings.EmailsVerificationSubject,
            AppStrings.EmailsVerificationHeading,
            AppStrings.EmailsVerificationIntroText,
            AppStrings.EmailsVerificationIntroHtml,
            AppStrings.EmailsVerificationExpiryText,
            AppStrings.EmailsVerificationExpiryHtml,
            AppStrings.EmailsVerificationIgnoreNote
        );

    /// <summary>
    /// Gets the texts of the link that confirms the new email an account holder asked for.
    /// </summary>
    internal static Texts EmailChange =>
        new(
            AppStrings.EmailsEmailChangeSubject,
            AppStrings.EmailsEmailChangeHeading,
            AppStrings.EmailsEmailChangeIntroText,
            AppStrings.EmailsEmailChangeIntroHtml,
            AppStrings.EmailsEmailChangeExpiryText,
            AppStrings.EmailsEmailChangeExpiryHtml,
            AppStrings.EmailsEmailChangeIgnoreNote
        );

    /// <summary>
    /// Gets the texts of the link that lets an account holder choose a new password.
    /// </summary>
    internal static Texts PasswordReset =>
        new(
            AppStrings.EmailsPasswordResetSubject,
            AppStrings.EmailsPasswordResetHeading,
            AppStrings.EmailsPasswordResetIntroText,
            AppStrings.EmailsPasswordResetIntroHtml,
            AppStrings.EmailsPasswordResetExpiryText,
            AppStrings.EmailsPasswordResetExpiryHtml,
            AppStrings.EmailsPasswordResetIgnoreNote
        );

    /// <summary>
    /// Gets the texts of the code that completes a login.
    /// </summary>
    internal static Texts LoginCode =>
        new(
            AppStrings.EmailsLoginCodeSubject,
            AppStrings.EmailsLoginCodeHeading,
            AppStrings.EmailsLoginCodeIntroText,
            AppStrings.EmailsLoginCodeIntroHtml,
            AppStrings.EmailsLoginCodeExpiryText,
            AppStrings.EmailsLoginCodeExpiryHtml,
            AppStrings.EmailsLoginCodeIgnoreNote
        );

    /// <summary>
    /// Gets the texts of the code that opens a consultation of the blocked data.
    /// </summary>
    internal static Texts BlockedDataConsultationCode =>
        new(
            AppStrings.EmailsBlockedDataConsultationCodeSubject,
            AppStrings.EmailsBlockedDataConsultationCodeHeading,
            AppStrings.EmailsBlockedDataConsultationCodeIntroText,
            AppStrings.EmailsBlockedDataConsultationCodeIntroHtml,
            AppStrings.EmailsBlockedDataConsultationCodeExpiryText,
            AppStrings.EmailsBlockedDataConsultationCodeExpiryHtml,
            AppStrings.EmailsBlockedDataConsultationCodeIgnoreNote
        );

    /// <summary>
    /// Gets the texts of the code that confirms a change of an account.
    /// </summary>
    /// <param name="purpose">Change the code confirms.</param>
    /// <returns>The texts that name that change.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The purpose is not a known one.</exception>
    internal static Texts ConfirmationCode(ConfirmationCodePurpose purpose)
    {
        return purpose switch
        {
            ConfirmationCodePurpose.AccountDeletion => new(
                AppStrings.EmailsAccountDeletionCodeSubject,
                AppStrings.EmailsAccountDeletionCodeHeading,
                AppStrings.EmailsAccountDeletionCodeIntroText,
                AppStrings.EmailsAccountDeletionCodeIntroHtml,
                AppStrings.EmailsAccountDeletionCodeExpiryText,
                AppStrings.EmailsAccountDeletionCodeExpiryHtml,
                AppStrings.EmailsAccountDeletionCodeIgnoreNote
            ),
            ConfirmationCodePurpose.EmailChange => new(
                AppStrings.EmailsEmailChangeCodeSubject,
                AppStrings.EmailsEmailChangeCodeHeading,
                AppStrings.EmailsEmailChangeCodeIntroText,
                AppStrings.EmailsEmailChangeCodeIntroHtml,
                AppStrings.EmailsEmailChangeCodeExpiryText,
                AppStrings.EmailsEmailChangeCodeExpiryHtml,
                AppStrings.EmailsEmailChangeCodeIgnoreNote
            ),
            ConfirmationCodePurpose.AuthenticatorSetup => new(
                AppStrings.EmailsAuthenticatorSetupCodeSubject,
                AppStrings.EmailsAuthenticatorSetupCodeHeading,
                AppStrings.EmailsAuthenticatorSetupCodeIntroText,
                AppStrings.EmailsAuthenticatorSetupCodeIntroHtml,
                AppStrings.EmailsAuthenticatorSetupCodeExpiryText,
                AppStrings.EmailsAuthenticatorSetupCodeExpiryHtml,
                AppStrings.EmailsAuthenticatorSetupCodeIgnoreNote
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(purpose),
                purpose,
                "The confirmation code purpose is not a known one."
            ),
        };
    }

    /// <summary>
    /// Creates the email.
    /// </summary>
    /// <param name="texts">Texts of the email.</param>
    /// <param name="kind">Kind of the message.</param>
    /// <param name="recipient">Address and name of the account holder.</param>
    /// <param name="credential">The code, or the button that opens the link.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <param name="lifetime">Validity of the code or link.</param>
    /// <param name="notes">Paragraphs shown after the validity, before the sign-off.</param>
    /// <returns>The message.</returns>
    internal static EmailMessage Create(
        Texts texts,
        EmailKind kind,
        EmailRecipient recipient,
        EmailBlock credential,
        string siteUrl,
        TimeSpan lifetime,
        params IEnumerable<EmailBlock> notes
    )
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(lifetime.TotalMinutes));

        var content = EmailLayout.Render(
            new EmailDocument(
                texts.Heading,
                texts.IntroText,
                recipient.Name,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                EmailBlocks.Prose(texts.IntroHtml, texts.IntroText),
                credential,
                EmailBlocks.Prose(texts.ExpiryHtml(minutes), texts.ExpiryText(minutes)),
                .. notes,
                EmailBlocks.Signoff(),
                EmailBlocks.Note(texts.IgnoreNote),
            ]
        );

        return content.ToMessage(kind, recipient.Address, recipient.Name, texts.Subject);
    }

    /// <summary>
    /// Texts of one of these emails.
    /// </summary>
    /// <param name="Subject">Subject line.</param>
    /// <param name="Heading">Heading of the body.</param>
    /// <param name="IntroText">Plain-text introduction, also the preview.</param>
    /// <param name="IntroHtml">HTML introduction.</param>
    /// <param name="ExpiryText">Plain-text validity, by minutes.</param>
    /// <param name="ExpiryHtml">HTML validity, by minutes.</param>
    /// <param name="IgnoreNote">Note for whoever did not ask for the email.</param>
    internal sealed record Texts(
        string Subject,
        string Heading,
        string IntroText,
        string IntroHtml,
        Func<int, string> ExpiryText,
        Func<int, string> ExpiryHtml,
        string IgnoreNote
    );
}
