using System.Globalization;
using System.Resources;

namespace CodigoActivo.Application.Common.Localization;

/// <summary>
/// Provides strongly typed access to localized application strings.
/// </summary>
public static class AppStrings
{
    /// <summary>
    /// Identifies the base name configuration or policy value.
    /// </summary>
    public const string BaseName = "CodigoActivo.Application.Common.Localization.AppStrings";
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly ResourceManager Manager = new(BaseName, typeof(AppStrings).Assembly);

    /// <summary>
    /// Gets the emails account deletion code heading value.
    /// </summary>
    public static string EmailsAccountDeletionCodeHeading =>
        Get("emails.accountDeletionCode.heading");

    /// <summary>
    /// Gets the emails account deletion code ignore note value.
    /// </summary>
    public static string EmailsAccountDeletionCodeIgnoreNote =>
        Get("emails.accountDeletionCode.ignoreNote");

    /// <summary>
    /// Gets the emails account deletion code intro html value.
    /// </summary>
    public static string EmailsAccountDeletionCodeIntroHtml =>
        Get("emails.accountDeletionCode.introHtml");

    /// <summary>
    /// Gets the emails account deletion code intro text value.
    /// </summary>
    public static string EmailsAccountDeletionCodeIntroText =>
        Get("emails.accountDeletionCode.introText");

    /// <summary>
    /// Gets the emails account deletion code subject value.
    /// </summary>
    public static string EmailsAccountDeletionCodeSubject =>
        Get("emails.accountDeletionCode.subject");

    /// <summary>
    /// Gets the emails activity decision confirmed button label value.
    /// </summary>
    public static string EmailsActivityDecisionConfirmedButtonLabel =>
        Get("emails.activityDecision.confirmedButtonLabel");

    /// <summary>
    /// Gets the emails activity decision confirmed heading value.
    /// </summary>
    public static string EmailsActivityDecisionConfirmedHeading =>
        Get("emails.activityDecision.confirmedHeading");

    /// <summary>
    /// Gets the emails activity decision confirmed note value.
    /// </summary>
    public static string EmailsActivityDecisionConfirmedNote =>
        Get("emails.activityDecision.confirmedNote");

    /// <summary>
    /// Gets the emails activity decision denied button label value.
    /// </summary>
    public static string EmailsActivityDecisionDeniedButtonLabel =>
        Get("emails.activityDecision.deniedButtonLabel");

    /// <summary>
    /// Gets the emails activity decision denied heading value.
    /// </summary>
    public static string EmailsActivityDecisionDeniedHeading =>
        Get("emails.activityDecision.deniedHeading");

    /// <summary>
    /// Gets the emails activity decision signup phrase self value.
    /// </summary>
    public static string EmailsActivityDecisionSignupPhraseSelf =>
        Get("emails.activityDecision.signupPhraseSelf");

    /// <summary>
    /// Gets the emails coming of age blocked data note value.
    /// </summary>
    public static string EmailsComingOfAgeBlockedCopyNote =>
        Get("emails.comingOfAge.blockedCopyNote");

    /// <summary>
    /// Gets the emails coming of age button label value.
    /// </summary>
    public static string EmailsComingOfAgeButtonLabel => Get("emails.comingOfAge.buttonLabel");

    /// <summary>
    /// Gets the emails coming of age download value.
    /// </summary>
    public static string EmailsComingOfAgeDownload => Get("emails.comingOfAge.download");

    /// <summary>
    /// Gets the emails coming of age heading value.
    /// </summary>
    public static string EmailsComingOfAgeHeading => Get("emails.comingOfAge.heading");

    /// <summary>
    /// Gets the emails coming of age own account value.
    /// </summary>
    public static string EmailsComingOfAgeOwnAccount => Get("emails.comingOfAge.ownAccount");

    /// <summary>
    /// Gets the emails coming of age subject value.
    /// </summary>
    public static string EmailsComingOfAgeSubject => Get("emails.comingOfAge.subject");

    /// <summary>
    /// Gets the emails consent withdrawn action value.
    /// </summary>
    public static string EmailsConsentWithdrawnAction => Get("emails.consentWithdrawn.action");

    /// <summary>
    /// Gets the subject of the notice of a consultation of the blocked data.
    /// </summary>
    public static string EmailsBlockedDataConsultationSubject =>
        Get("emails.blockedDataConsultation.subject");

    /// <summary>
    /// Gets the heading of the notice of a consultation of the blocked data.
    /// </summary>
    public static string EmailsBlockedDataConsultationHeading =>
        Get("emails.blockedDataConsultation.heading");

    /// <summary>
    /// Gets the closing note of the notice of a consultation of the blocked data.
    /// </summary>
    public static string EmailsBlockedDataConsultationNote =>
        Get("emails.blockedDataConsultation.note");

    /// <summary>
    /// Gets the name of a court as the authority of a consultation of the blocked data.
    /// </summary>
    public static string EmailsBlockedDataConsultationAuthorityCourt =>
        Get("emails.blockedDataConsultation.authority.Court");

    /// <summary>
    /// Gets the name of the public prosecutor as the authority of a consultation.
    /// </summary>
    public static string EmailsBlockedDataConsultationAuthorityProsecutor =>
        Get("emails.blockedDataConsultation.authority.Prosecutor");

    /// <summary>
    /// Gets the name of the data protection authority as the authority of a consultation.
    /// </summary>
    public static string EmailsBlockedDataConsultationAuthorityDataProtectionAuthority =>
        Get("emails.blockedDataConsultation.authority.DataProtectionAuthority");

    /// <summary>
    /// Gets the name of another public administration as the authority of a consultation.
    /// </summary>
    public static string EmailsBlockedDataConsultationAuthorityPublicAdministration =>
        Get("emails.blockedDataConsultation.authority.PublicAdministration");

    /// <summary>
    /// Gets the name of a claim against the association as the reason of a consultation.
    /// </summary>
    public static string EmailsBlockedDataConsultationAuthorityClaim =>
        Get("emails.blockedDataConsultation.authority.Claim");

    /// <summary>
    /// Gets the emails consent withdrawn heading value.
    /// </summary>
    public static string EmailsConsentWithdrawnHeading => Get("emails.consentWithdrawn.heading");

    /// <summary>
    /// Gets the emails consent withdrawn history note value.
    /// </summary>
    public static string EmailsConsentWithdrawnHistoryNote =>
        Get("emails.consentWithdrawn.historyNote");

    /// <summary>
    /// Gets the emails consent withdrawn subject value.
    /// </summary>
    public static string EmailsConsentWithdrawnSubject => Get("emails.consentWithdrawn.subject");

    /// <summary>
    /// Gets the emails details activity label value.
    /// </summary>
    public static string EmailsDetailsActivityLabel => Get("emails.details.activityLabel");

    /// <summary>
    /// Gets the emails details event label value.
    /// </summary>
    public static string EmailsDetailsEventLabel => Get("emails.details.eventLabel");

    /// <summary>
    /// Gets the emails details location label value.
    /// </summary>
    public static string EmailsDetailsLocationLabel => Get("emails.details.locationLabel");

    /// <summary>
    /// Gets the emails details role label value.
    /// </summary>
    public static string EmailsDetailsRoleLabel => Get("emails.details.roleLabel");

    /// <summary>
    /// Gets the emails details schedule label value.
    /// </summary>
    public static string EmailsDetailsScheduleLabel => Get("emails.details.scheduleLabel");

    /// <summary>
    /// Gets the emails email change button label value.
    /// </summary>
    public static string EmailsEmailChangeButtonLabel => Get("emails.emailChange.buttonLabel");

    /// <summary>
    /// Gets the emails email change heading value.
    /// </summary>
    public static string EmailsEmailChangeHeading => Get("emails.emailChange.heading");

    /// <summary>
    /// Gets the emails email change ignore note value.
    /// </summary>
    public static string EmailsEmailChangeIgnoreNote => Get("emails.emailChange.ignoreNote");

    /// <summary>
    /// Gets the emails email change intro html value.
    /// </summary>
    public static string EmailsEmailChangeIntroHtml => Get("emails.emailChange.introHtml");

    /// <summary>
    /// Gets the emails email change intro text value.
    /// </summary>
    public static string EmailsEmailChangeIntroText => Get("emails.emailChange.introText");

    /// <summary>
    /// Gets the emails email change subject value.
    /// </summary>
    public static string EmailsEmailChangeSubject => Get("emails.emailChange.subject");

    /// <summary>
    /// Gets the emails email in use button label value.
    /// </summary>
    public static string EmailsEmailInUseButtonLabel => Get("emails.emailInUse.buttonLabel");

    /// <summary>
    /// Gets the emails email in use heading value.
    /// </summary>
    public static string EmailsEmailInUseHeading => Get("emails.emailInUse.heading");

    /// <summary>
    /// Gets the emails email in use ignore note value.
    /// </summary>
    public static string EmailsEmailInUseIgnoreNote => Get("emails.emailInUse.ignoreNote");

    /// <summary>
    /// Gets the emails email in use intro html value.
    /// </summary>
    public static string EmailsEmailInUseIntroHtml => Get("emails.emailInUse.introHtml");

    /// <summary>
    /// Gets the emails email in use intro text value.
    /// </summary>
    public static string EmailsEmailInUseIntroText => Get("emails.emailInUse.introText");

    /// <summary>
    /// Gets the emails email in use recovery value.
    /// </summary>
    public static string EmailsEmailInUseRecovery => Get("emails.emailInUse.recovery");

    /// <summary>
    /// Gets the emails email in use subject value.
    /// </summary>
    public static string EmailsEmailInUseSubject => Get("emails.emailInUse.subject");

    /// <summary>
    /// Gets the emails footer tagline value.
    /// </summary>
    public static string EmailsFooterTagline => Get("emails.footer.tagline");

    /// <summary>
    /// Gets the emails footer website label value.
    /// </summary>
    public static string EmailsFooterWebsiteLabel => Get("emails.footer.websiteLabel");

    /// <summary>
    /// Gets why a news email reaches its recipient.
    /// </summary>
    public static string EmailsFooterNewsNotice => Get("emails.footer.newsNotice");

    /// <summary>
    /// Gets the label of the unsubscribe link of a news email.
    /// </summary>
    public static string EmailsFooterUnsubscribeLabel => Get("emails.footer.unsubscribeLabel");

    /// <summary>
    /// Gets the emails login code heading value.
    /// </summary>
    public static string EmailsLoginCodeHeading => Get("emails.loginCode.heading");

    /// <summary>
    /// Gets the emails login code ignore note value.
    /// </summary>
    public static string EmailsLoginCodeIgnoreNote => Get("emails.loginCode.ignoreNote");

    /// <summary>
    /// Gets the emails login code intro html value.
    /// </summary>
    public static string EmailsLoginCodeIntroHtml => Get("emails.loginCode.introHtml");

    /// <summary>
    /// Gets the emails login code intro text value.
    /// </summary>
    public static string EmailsLoginCodeIntroText => Get("emails.loginCode.introText");

    /// <summary>
    /// Gets the emails login code subject value.
    /// </summary>
    public static string EmailsLoginCodeSubject => Get("emails.loginCode.subject");

    /// <summary>
    /// Gets the emails password reset button label value.
    /// </summary>
    public static string EmailsPasswordResetButtonLabel => Get("emails.passwordReset.buttonLabel");

    /// <summary>
    /// Gets the emails password reset heading value.
    /// </summary>
    public static string EmailsPasswordResetHeading => Get("emails.passwordReset.heading");

    /// <summary>
    /// Gets the emails password reset ignore note value.
    /// </summary>
    public static string EmailsPasswordResetIgnoreNote => Get("emails.passwordReset.ignoreNote");

    /// <summary>
    /// Gets the emails password reset intro html value.
    /// </summary>
    public static string EmailsPasswordResetIntroHtml => Get("emails.passwordReset.introHtml");

    /// <summary>
    /// Gets the emails password reset intro text value.
    /// </summary>
    public static string EmailsPasswordResetIntroText => Get("emails.passwordReset.introText");

    /// <summary>
    /// Gets the emails password reset subject value.
    /// </summary>
    public static string EmailsPasswordResetSubject => Get("emails.passwordReset.subject");

    /// <summary>
    /// Gets the emails security alert admin granted value.
    /// </summary>
    public static string EmailsSecurityAlertAdminGranted =>
        Get("emails.securityAlert.adminGranted");

    /// <summary>
    /// Gets the emails security alert admin revoked value.
    /// </summary>
    public static string EmailsSecurityAlertAdminRevoked =>
        Get("emails.securityAlert.adminRevoked");

    /// <summary>
    /// Gets the emails security alert authenticator disabled value.
    /// </summary>
    public static string EmailsSecurityAlertAuthenticatorDisabled =>
        Get("emails.securityAlert.authenticatorDisabled");

    /// <summary>
    /// Gets the emails security alert authenticator enabled value.
    /// </summary>
    public static string EmailsSecurityAlertAuthenticatorEnabled =>
        Get("emails.securityAlert.authenticatorEnabled");

    /// <summary>
    /// Gets the emails security alert heading value.
    /// </summary>
    public static string EmailsSecurityAlertHeading => Get("emails.securityAlert.heading");

    /// <summary>
    /// Gets the emails security alert email changed value.
    /// </summary>
    public static string EmailsSecurityAlertEmailChanged =>
        Get("emails.securityAlert.emailChanged");

    /// <summary>
    /// Gets the emails security alert phone changed value.
    /// </summary>
    public static string EmailsSecurityAlertPhoneChanged =>
        Get("emails.securityAlert.phoneChanged");

    /// <summary>
    /// Gets the emails security alert email and phone changed value.
    /// </summary>
    public static string EmailsSecurityAlertEmailAndPhoneChanged =>
        Get("emails.securityAlert.emailAndPhoneChanged");

    /// <summary>
    /// Gets the emails security alert intro html value.
    /// </summary>
    public static string EmailsSecurityAlertIntroHtml => Get("emails.securityAlert.introHtml");

    /// <summary>
    /// Gets the emails security alert intro text value.
    /// </summary>
    public static string EmailsSecurityAlertIntroText => Get("emails.securityAlert.introText");

    /// <summary>
    /// Gets the advice for an email change the owner did not make, which a password reset can no
    /// longer undo because the reset link would reach the new address.
    /// </summary>
    public static string EmailsSecurityAlertEmailChangedWarning =>
        Get("emails.securityAlert.emailChangedWarning");

    /// <summary>
    /// Gets the advice for a change an administrator made on the account.
    /// </summary>
    public static string EmailsSecurityAlertUnexpectedWarning =>
        Get("emails.securityAlert.unexpectedWarning");

    /// <summary>
    /// Gets the advice for a password lock caused by someone else's attempts.
    /// </summary>
    public static string EmailsSecurityAlertPasswordLockedWarning =>
        Get("emails.securityAlert.passwordLockedWarning");

    /// <summary>
    /// Gets the emails security alert password changed value.
    /// </summary>
    public static string EmailsSecurityAlertPasswordChanged =>
        Get("emails.securityAlert.passwordChanged");

    /// <summary>
    /// Gets the emails security alert password locked value.
    /// </summary>
    public static string EmailsSecurityAlertPasswordLocked =>
        Get("emails.securityAlert.passwordLocked");

    /// <summary>
    /// Gets the emails security alert password reset value.
    /// </summary>
    public static string EmailsSecurityAlertPasswordReset =>
        Get("emails.securityAlert.passwordReset");

    /// <summary>
    /// Gets the emails security alert subject value.
    /// </summary>
    public static string EmailsSecurityAlertSubject => Get("emails.securityAlert.subject");

    /// <summary>
    /// Gets the subject of the alert sent when failed password attempts lock the account.
    /// </summary>
    public static string EmailsSecurityAlertPasswordLockedSubject =>
        Get("emails.securityAlert.passwordLockedSubject");

    /// <summary>
    /// Gets the heading of the alert sent when failed password attempts lock the account.
    /// </summary>
    public static string EmailsSecurityAlertPasswordLockedHeading =>
        Get("emails.securityAlert.passwordLockedHeading");

    /// <summary>
    /// Gets the emails security alert two factor reset value.
    /// </summary>
    public static string EmailsSecurityAlertTwoFactorReset =>
        Get("emails.securityAlert.twoFactorReset");

    /// <summary>
    /// Gets the emails security alert warning value.
    /// </summary>
    public static string EmailsSecurityAlertWarning => Get("emails.securityAlert.warning");

    /// <summary>
    /// Gets the emails shared fallback link note value.
    /// </summary>
    public static string EmailsSharedFallbackLinkNote => Get("emails.shared.fallbackLinkNote");

    /// <summary>
    /// Gets the emails shared logo alt value.
    /// </summary>
    public static string EmailsSharedLogoAlt => Get("emails.shared.logoAlt");

    /// <summary>
    /// Gets the emails shared signoff html value.
    /// </summary>
    public static string EmailsSharedSignoffHtml => Get("emails.shared.signoffHtml");

    /// <summary>
    /// Gets the emails shared signoff text value.
    /// </summary>
    public static string EmailsSharedSignoffText => Get("emails.shared.signoffText");

    /// <summary>
    /// Gets the emails verification button label value.
    /// </summary>
    public static string EmailsVerificationButtonLabel => Get("emails.verification.buttonLabel");

    /// <summary>
    /// Gets the emails verification heading value.
    /// </summary>
    public static string EmailsVerificationHeading => Get("emails.verification.heading");

    /// <summary>
    /// Gets the emails verification ignore note value.
    /// </summary>
    public static string EmailsVerificationIgnoreNote => Get("emails.verification.ignoreNote");

    /// <summary>
    /// Gets the paragraph that reminds the news consent given at registration.
    /// </summary>
    public static string EmailsVerificationNewsConsent => Get("emails.verification.newsConsent");

    /// <summary>
    /// Gets the emails verification intro html value.
    /// </summary>
    public static string EmailsVerificationIntroHtml => Get("emails.verification.introHtml");

    /// <summary>
    /// Gets the emails verification intro text value.
    /// </summary>
    public static string EmailsVerificationIntroText => Get("emails.verification.introText");

    /// <summary>
    /// Gets the emails verification subject value.
    /// </summary>
    public static string EmailsVerificationSubject => Get("emails.verification.subject");

    /// <summary>
    /// Gets the files fallback attachment name value.
    /// </summary>
    public static string FilesFallbackAttachmentName => Get("files.fallbackAttachmentName");

    /// <summary>
    /// Gets the files fallback file name value.
    /// </summary>
    public static string FilesFallbackFileName => Get("files.fallbackFileName");

    /// <summary>
    /// Gets the name of the cookie policy.
    /// </summary>
    public static string LegalPoliciesCookies => Get("legal.policies.cookies");

    /// <summary>
    /// Gets the name of the privacy policy.
    /// </summary>
    public static string LegalPoliciesPrivacy => Get("legal.policies.privacy");

    /// <summary>
    /// Formats the localized emails account deletion code expiry html text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsAccountDeletionCodeExpiryHtml(int minutes)
    {
        return Format("emails.accountDeletionCode.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the localized emails account deletion code expiry text text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsAccountDeletionCodeExpiryText(int minutes)
    {
        return Format("emails.accountDeletionCode.expiryText", minutes);
    }

    /// <summary>
    /// Formats the localized emails activity decision confirmed intro text with the supplied values.
    /// </summary>
    /// <param name="signupPhrase">The signup phrase value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionConfirmedIntro(string signupPhrase)
    {
        return Format("emails.activityDecision.confirmedIntro", signupPhrase);
    }

    /// <summary>
    /// Formats the localized emails activity decision confirmed subject text with the supplied values.
    /// </summary>
    /// <param name="activityTitle">The activity title value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionConfirmedSubject(string activityTitle)
    {
        return Format("emails.activityDecision.confirmedSubject", activityTitle);
    }

    /// <summary>
    /// Formats the localized emails activity decision denied intro text with the supplied values.
    /// </summary>
    /// <param name="signupPhrase">The signup phrase value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionDeniedIntro(string signupPhrase)
    {
        return Format("emails.activityDecision.deniedIntro", signupPhrase);
    }

    /// <summary>
    /// Formats the localized emails activity decision denied subject text with the supplied values.
    /// </summary>
    /// <param name="activityTitle">The activity title value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionDeniedSubject(string activityTitle)
    {
        return Format("emails.activityDecision.deniedSubject", activityTitle);
    }

    /// <summary>
    /// Formats the localized emails activity decision signup phrase named text with the supplied values.
    /// </summary>
    /// <param name="participantName">The participant name value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionSignupPhraseNamed(string participantName)
    {
        return Format("emails.activityDecision.signupPhraseNamed", participantName);
    }

    /// <summary>
    /// Formats the localized emails coming of age intro html text with the supplied values.
    /// </summary>
    /// <param name="dependentName">Name of the dependent, already HTML-encoded.</param>
    /// <param name="comingOfAgeOn">Day the dependent comes of age.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsComingOfAgeIntroHtml(string dependentName, string comingOfAgeOn)
    {
        return Format("emails.comingOfAge.introHtml", dependentName, comingOfAgeOn);
    }

    /// <summary>
    /// Formats the localized emails coming of age intro text text with the supplied values.
    /// </summary>
    /// <param name="dependentName">Name of the dependent.</param>
    /// <param name="comingOfAgeOn">Day the dependent comes of age.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsComingOfAgeIntroText(string dependentName, string comingOfAgeOn)
    {
        return Format("emails.comingOfAge.introText", dependentName, comingOfAgeOn);
    }

    /// <summary>
    /// Formats the localized emails consent withdrawn intro html text with the supplied values.
    /// </summary>
    /// <param name="personName">Name of the person, already HTML-encoded.</param>
    /// <param name="documentName">Name of the document, already HTML-encoded.</param>
    /// <param name="eventTitle">Title of the event, already HTML-encoded.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsConsentWithdrawnIntroHtml(
        string personName,
        string documentName,
        string eventTitle
    )
    {
        return Format("emails.consentWithdrawn.introHtml", personName, documentName, eventTitle);
    }

    /// <summary>
    /// Formats the opening line of the notice of a consultation of the blocked data, as text.
    /// </summary>
    /// <param name="officerName">Name of the account that opened it.</param>
    /// <param name="authority">Who the data is made available to, already named.</param>
    /// <param name="reference">Reference of the request.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsBlockedDataConsultationIntroText(
        string officerName,
        string authority,
        string reference
    )
    {
        return Format(
            "emails.blockedDataConsultation.introText",
            officerName,
            authority,
            reference
        );
    }

    /// <summary>
    /// Formats the opening line of the notice of a consultation of the blocked data, as HTML.
    /// </summary>
    /// <param name="officerName">Name of the account that opened it, already encoded.</param>
    /// <param name="authority">Who the data is made available to, already encoded.</param>
    /// <param name="reference">Reference of the request, already encoded.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsBlockedDataConsultationIntroHtml(
        string officerName,
        string authority,
        string reference
    )
    {
        return Format(
            "emails.blockedDataConsultation.introHtml",
            officerName,
            authority,
            reference
        );
    }

    /// <summary>
    /// Formats when a consultation of the blocked data was opened.
    /// </summary>
    /// <param name="openedAt">When it was opened, already formatted.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsBlockedDataConsultationOpened(string openedAt)
    {
        return Format("emails.blockedDataConsultation.opened", openedAt);
    }

    /// <summary>
    /// Formats the localized emails consent withdrawn intro text text with the supplied values.
    /// </summary>
    /// <param name="personName">Name of the person.</param>
    /// <param name="documentName">Name of the document.</param>
    /// <param name="eventTitle">Title of the event.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsConsentWithdrawnIntroText(
        string personName,
        string documentName,
        string eventTitle
    )
    {
        return Format("emails.consentWithdrawn.introText", personName, documentName, eventTitle);
    }

    /// <summary>
    /// Formats the localized emails consent withdrawn recorded text with the supplied values.
    /// </summary>
    /// <param name="recordedByName">Name of the account that recorded the withdrawal.</param>
    /// <param name="withdrawnAt">When it was withdrawn, already formatted.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsConsentWithdrawnRecorded(string recordedByName, string withdrawnAt)
    {
        return Format("emails.consentWithdrawn.recorded", recordedByName, withdrawnAt);
    }

    /// <summary>
    /// Formats the localized emails details row text text with the supplied values.
    /// </summary>
    /// <param name="label">The label value.</param>
    /// <param name="value">Value to validate or convert.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsDetailsRowText(string label, string value)
    {
        return Format("emails.details.rowText", label, value);
    }

    /// <summary>
    /// Formats the localized emails details schedule multi day text with the supplied values.
    /// </summary>
    /// <param name="startDate">The start date value.</param>
    /// <param name="startTime">The start time value.</param>
    /// <param name="endDate">The end date value.</param>
    /// <param name="endTime">The end time value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsDetailsScheduleMultiDay(
        string startDate,
        string startTime,
        string endDate,
        string endTime
    )
    {
        return Format("emails.details.scheduleMultiDay", startDate, startTime, endDate, endTime);
    }

    /// <summary>
    /// Formats the localized emails details schedule same day text with the supplied values.
    /// </summary>
    /// <param name="startDate">The start date value.</param>
    /// <param name="startTime">The start time value.</param>
    /// <param name="endTime">The end time value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsDetailsScheduleSameDay(
        string startDate,
        string startTime,
        string endTime
    )
    {
        return Format("emails.details.scheduleSameDay", startDate, startTime, endTime);
    }

    /// <summary>
    /// Formats the localized emails email change expiry html text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsEmailChangeExpiryHtml(int minutes)
    {
        return Format("emails.emailChange.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the localized emails email change expiry text text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsEmailChangeExpiryText(int minutes)
    {
        return Format("emails.emailChange.expiryText", minutes);
    }

    /// <summary>
    /// Formats how a news email offers the contact address to unsubscribe.
    /// </summary>
    /// <param name="address">Contact address, or the markup that links it.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsFooterContact(string address)
    {
        return Format("emails.footer.contact", address);
    }

    /// <summary>
    /// Formats how the text body of a news email offers its unsubscribe link.
    /// </summary>
    /// <param name="url">Unsubscribe link.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsFooterUnsubscribeText(string url)
    {
        return Format("emails.footer.unsubscribeText", url);
    }

    /// <summary>
    /// Formats the localized emails login code expiry html text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsLoginCodeExpiryHtml(int minutes)
    {
        return Format("emails.loginCode.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the localized emails login code expiry text text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsLoginCodeExpiryText(int minutes)
    {
        return Format("emails.loginCode.expiryText", minutes);
    }

    /// <summary>
    /// Formats the localized emails password reset expiry html text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsPasswordResetExpiryHtml(int minutes)
    {
        return Format("emails.passwordReset.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the localized emails password reset expiry text text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsPasswordResetExpiryText(int minutes)
    {
        return Format("emails.passwordReset.expiryText", minutes);
    }

    /// <summary>
    /// Formats the localized emails security alert new email text with the supplied values.
    /// </summary>
    /// <param name="maskedEmail">The masked email value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsSecurityAlertNewEmail(string maskedEmail)
    {
        return Format("emails.securityAlert.newEmail", maskedEmail);
    }

    /// <summary>
    /// Formats the localized emails security alert when text with the supplied values.
    /// </summary>
    /// <param name="occurredAt">The occurred at value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsSecurityAlertWhen(string occurredAt)
    {
        return Format("emails.securityAlert.when", occurredAt);
    }

    /// <summary>
    /// Formats the localized emails shared greeting text with the supplied values.
    /// </summary>
    /// <param name="name">The name value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsSharedGreeting(string name)
    {
        return Format("emails.shared.greeting", name);
    }

    /// <summary>
    /// Formats the localized emails verification expiry html text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsVerificationExpiryHtml(int minutes)
    {
        return Format("emails.verification.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the localized emails verification expiry text text with the supplied values.
    /// </summary>
    /// <param name="minutes">The minutes value.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsVerificationExpiryText(int minutes)
    {
        return Format("emails.verification.expiryText", minutes);
    }

    private static string Format(string key, params object?[] arguments)
    {
        return string.Format(Culture, Get(key), arguments);
    }

    private static string Get(string key)
    {
        var value = Manager.GetString(key, Culture);
        return string.IsNullOrEmpty(value)
            ? throw new InvalidOperationException($"Missing resource string '{key}'.")
            : value;
    }
}
