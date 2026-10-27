using System.Globalization;
using System.Resources;

namespace CodigoActivo.Application.Common.Localization;

/// <summary>
/// Provides strongly typed access to localized application strings.
/// </summary>
public static class AppStrings
{
    /// <summary>
    /// Base name of the <c>AppStrings.resx</c> resources, which hold the Spanish texts.
    /// </summary>
    public const string BaseName = "CodigoActivo.Application.Common.Localization.AppStrings";
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly ResourceManager Manager = new(BaseName, typeof(AppStrings).Assembly);

    /// <summary>
    /// Gets the heading of the email with the code that confirms deleting one's account.
    /// </summary>
    public static string EmailsAccountDeletionCodeHeading =>
        Get("emails.accountDeletionCode.heading");

    /// <summary>
    /// Gets the closing note of the account deletion code email for whoever did not ask for it.
    /// </summary>
    public static string EmailsAccountDeletionCodeIgnoreNote =>
        Get("emails.accountDeletionCode.ignoreNote");

    /// <summary>
    /// Gets the HTML introduction of the account deletion code email.
    /// </summary>
    public static string EmailsAccountDeletionCodeIntroHtml =>
        Get("emails.accountDeletionCode.introHtml");

    /// <summary>
    /// Gets the plain-text introduction of the account deletion code email.
    /// </summary>
    public static string EmailsAccountDeletionCodeIntroText =>
        Get("emails.accountDeletionCode.introText");

    /// <summary>
    /// Gets the subject of the account deletion code email.
    /// </summary>
    public static string EmailsAccountDeletionCodeSubject =>
        Get("emails.accountDeletionCode.subject");

    /// <summary>
    /// Gets the button label of the email that confirms an activity signup.
    /// </summary>
    public static string EmailsActivityDecisionConfirmedButtonLabel =>
        Get("emails.activityDecision.confirmedButtonLabel");

    /// <summary>
    /// Gets the heading of the email that confirms an activity signup.
    /// </summary>
    public static string EmailsActivityDecisionConfirmedHeading =>
        Get("emails.activityDecision.confirmedHeading");

    /// <summary>
    /// Gets the note of the email that confirms an activity signup, asking to cancel it in time.
    /// </summary>
    public static string EmailsActivityDecisionConfirmedNote =>
        Get("emails.activityDecision.confirmedNote");

    /// <summary>
    /// Gets the button label of the email that denies an activity signup.
    /// </summary>
    public static string EmailsActivityDecisionDeniedButtonLabel =>
        Get("emails.activityDecision.deniedButtonLabel");

    /// <summary>
    /// Gets the heading of the email that denies an activity signup.
    /// </summary>
    public static string EmailsActivityDecisionDeniedHeading =>
        Get("emails.activityDecision.deniedHeading");

    /// <summary>
    /// Gets how an activity decision email names the signup of its recipient.
    /// </summary>
    public static string EmailsActivityDecisionSignupPhraseSelf =>
        Get("emails.activityDecision.signupPhraseSelf");

    /// <summary>
    /// Gets the note of the coming-of-age email on the data kept blocked after the deletion.
    /// </summary>
    public static string EmailsComingOfAgeBlockedCopyNote =>
        Get("emails.comingOfAge.blockedCopyNote");

    /// <summary>
    /// Gets the button label of the email that tells a guardian a dependent comes of age.
    /// </summary>
    public static string EmailsComingOfAgeButtonLabel => Get("emails.comingOfAge.buttonLabel");

    /// <summary>
    /// Gets the line of the coming-of-age email on downloading a copy of the dependent's data.
    /// </summary>
    public static string EmailsComingOfAgeDownload => Get("emails.comingOfAge.download");

    /// <summary>
    /// Gets the heading of the coming-of-age email.
    /// </summary>
    public static string EmailsComingOfAgeHeading => Get("emails.comingOfAge.heading");

    /// <summary>
    /// Gets the line of the coming-of-age email on registering an account of one's own.
    /// </summary>
    public static string EmailsComingOfAgeOwnAccount => Get("emails.comingOfAge.ownAccount");

    /// <summary>
    /// Gets the subject of the coming-of-age email.
    /// </summary>
    public static string EmailsComingOfAgeSubject => Get("emails.comingOfAge.subject");

    /// <summary>
    /// Gets what the notice of a withdrawn consent tells the association to do from now on.
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
    /// Gets the heading of the notice of several consultations of the blocked data.
    /// </summary>
    public static string EmailsBlockedDataConsultationHeadingMany =>
        Get("emails.blockedDataConsultation.headingMany");

    /// <summary>
    /// Gets the closing note of the notice of several consultations of the blocked data.
    /// </summary>
    public static string EmailsBlockedDataConsultationNoteMany =>
        Get("emails.blockedDataConsultation.noteMany");

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
    /// Gets the subject of the code that confirms opening a consultation of the blocked data.
    /// </summary>
    public static string EmailsBlockedDataConsultationCodeSubject =>
        Get("emails.blockedDataConsultationCode.subject");

    /// <summary>
    /// Gets the heading of the code that confirms opening a consultation of the blocked data.
    /// </summary>
    public static string EmailsBlockedDataConsultationCodeHeading =>
        Get("emails.blockedDataConsultationCode.heading");

    /// <summary>
    /// Gets the plain-text introduction of the code that confirms opening a consultation.
    /// </summary>
    public static string EmailsBlockedDataConsultationCodeIntroText =>
        Get("emails.blockedDataConsultationCode.introText");

    /// <summary>
    /// Gets the HTML introduction of the code that confirms opening a consultation.
    /// </summary>
    public static string EmailsBlockedDataConsultationCodeIntroHtml =>
        Get("emails.blockedDataConsultationCode.introHtml");

    /// <summary>
    /// Gets the closing note of the code that confirms opening a consultation.
    /// </summary>
    public static string EmailsBlockedDataConsultationCodeIgnoreNote =>
        Get("emails.blockedDataConsultationCode.ignoreNote");

    /// <summary>
    /// Gets the heading of the notice of a withdrawn consent.
    /// </summary>
    public static string EmailsConsentWithdrawnHeading => Get("emails.consentWithdrawn.heading");

    /// <summary>
    /// Gets the note of the notice of a withdrawn consent on where the decision stays recorded.
    /// </summary>
    public static string EmailsConsentWithdrawnHistoryNote =>
        Get("emails.consentWithdrawn.historyNote");

    /// <summary>
    /// Gets the subject of the notice of a withdrawn consent.
    /// </summary>
    public static string EmailsConsentWithdrawnSubject => Get("emails.consentWithdrawn.subject");

    /// <summary>
    /// Gets what the notice of several withdrawn consents tells the association to do from now on.
    /// </summary>
    public static string EmailsConsentWithdrawnActionMany =>
        Get("emails.consentWithdrawn.actionMany");

    /// <summary>
    /// Gets the heading of the notice of several withdrawn consents.
    /// </summary>
    public static string EmailsConsentWithdrawnHeadingMany =>
        Get("emails.consentWithdrawn.headingMany");

    /// <summary>
    /// Gets the note of the notice of several withdrawn consents on where the decisions stay
    /// recorded.
    /// </summary>
    public static string EmailsConsentWithdrawnHistoryNoteMany =>
        Get("emails.consentWithdrawn.historyNoteMany");

    /// <summary>
    /// Gets the label of the activity row in the details block of an email.
    /// </summary>
    public static string EmailsDetailsActivityLabel => Get("emails.details.activityLabel");

    /// <summary>
    /// Gets the label of the event row in the details block of an email.
    /// </summary>
    public static string EmailsDetailsEventLabel => Get("emails.details.eventLabel");

    /// <summary>
    /// Gets the label of the location row in the details block of an email.
    /// </summary>
    public static string EmailsDetailsLocationLabel => Get("emails.details.locationLabel");

    /// <summary>
    /// Gets the label of the role row in the details block of an email.
    /// </summary>
    public static string EmailsDetailsRoleLabel => Get("emails.details.roleLabel");

    /// <summary>
    /// Gets the label of the schedule row in the details block of an email.
    /// </summary>
    public static string EmailsDetailsScheduleLabel => Get("emails.details.scheduleLabel");

    /// <summary>
    /// Gets the button label of the email that confirms a new email address.
    /// </summary>
    public static string EmailsEmailChangeButtonLabel => Get("emails.emailChange.buttonLabel");

    /// <summary>
    /// Gets the heading of the email that confirms a new email address.
    /// </summary>
    public static string EmailsEmailChangeHeading => Get("emails.emailChange.heading");

    /// <summary>
    /// Gets the closing note of the email change confirmation for whoever did not ask for it.
    /// </summary>
    public static string EmailsEmailChangeIgnoreNote => Get("emails.emailChange.ignoreNote");

    /// <summary>
    /// Gets the HTML introduction of the email change confirmation.
    /// </summary>
    public static string EmailsEmailChangeIntroHtml => Get("emails.emailChange.introHtml");

    /// <summary>
    /// Gets the plain-text introduction of the email change confirmation.
    /// </summary>
    public static string EmailsEmailChangeIntroText => Get("emails.emailChange.introText");

    /// <summary>
    /// Gets the subject of the email change confirmation.
    /// </summary>
    public static string EmailsEmailChangeSubject => Get("emails.emailChange.subject");

    /// <summary>
    /// Gets the button label of the notice sent to the holder of an email someone tried to use.
    /// </summary>
    public static string EmailsEmailInUseButtonLabel => Get("emails.emailInUse.buttonLabel");

    /// <summary>
    /// Gets the heading of the email-in-use notice.
    /// </summary>
    public static string EmailsEmailInUseHeading => Get("emails.emailInUse.heading");

    /// <summary>
    /// Gets the closing note of the email-in-use notice for whoever did not cause it.
    /// </summary>
    public static string EmailsEmailInUseIgnoreNote => Get("emails.emailInUse.ignoreNote");

    /// <summary>
    /// Gets the HTML introduction of the email-in-use notice.
    /// </summary>
    public static string EmailsEmailInUseIntroHtml => Get("emails.emailInUse.introHtml");

    /// <summary>
    /// Gets the plain-text introduction of the email-in-use notice.
    /// </summary>
    public static string EmailsEmailInUseIntroText => Get("emails.emailInUse.introText");

    /// <summary>
    /// Gets the line of the email-in-use notice on recovering a forgotten password.
    /// </summary>
    public static string EmailsEmailInUseRecovery => Get("emails.emailInUse.recovery");

    /// <summary>
    /// Gets the subject of the email-in-use notice.
    /// </summary>
    public static string EmailsEmailInUseSubject => Get("emails.emailInUse.subject");

    /// <summary>
    /// Gets the tagline of the association in the email footer.
    /// </summary>
    public static string EmailsFooterTagline => Get("emails.footer.tagline");

    /// <summary>
    /// Gets the label of the website link in the email footer.
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
    /// Gets the heading of the email with a login code.
    /// </summary>
    public static string EmailsLoginCodeHeading => Get("emails.loginCode.heading");

    /// <summary>
    /// Gets the closing note of the login code email for whoever did not try to sign in.
    /// </summary>
    public static string EmailsLoginCodeIgnoreNote => Get("emails.loginCode.ignoreNote");

    /// <summary>
    /// Gets the HTML introduction of the login code email.
    /// </summary>
    public static string EmailsLoginCodeIntroHtml => Get("emails.loginCode.introHtml");

    /// <summary>
    /// Gets the plain-text introduction of the login code email.
    /// </summary>
    public static string EmailsLoginCodeIntroText => Get("emails.loginCode.introText");

    /// <summary>
    /// Gets the subject of the login code email.
    /// </summary>
    public static string EmailsLoginCodeSubject => Get("emails.loginCode.subject");

    /// <summary>
    /// Gets the button label of the password reset email.
    /// </summary>
    public static string EmailsPasswordResetButtonLabel => Get("emails.passwordReset.buttonLabel");

    /// <summary>
    /// Gets the heading of the password reset email.
    /// </summary>
    public static string EmailsPasswordResetHeading => Get("emails.passwordReset.heading");

    /// <summary>
    /// Gets the closing note of the password reset email for whoever did not ask for it.
    /// </summary>
    public static string EmailsPasswordResetIgnoreNote => Get("emails.passwordReset.ignoreNote");

    /// <summary>
    /// Gets the HTML introduction of the password reset email.
    /// </summary>
    public static string EmailsPasswordResetIntroHtml => Get("emails.passwordReset.introHtml");

    /// <summary>
    /// Gets the plain-text introduction of the password reset email.
    /// </summary>
    public static string EmailsPasswordResetIntroText => Get("emails.passwordReset.introText");

    /// <summary>
    /// Gets the subject of the password reset email.
    /// </summary>
    public static string EmailsPasswordResetSubject => Get("emails.passwordReset.subject");

    /// <summary>
    /// Gets the security alert line for administrator rights granted to the account.
    /// </summary>
    public static string EmailsSecurityAlertAdminGranted =>
        Get("emails.securityAlert.adminGranted");

    /// <summary>
    /// Gets the security alert line for administrator rights revoked from the account.
    /// </summary>
    public static string EmailsSecurityAlertAdminRevoked =>
        Get("emails.securityAlert.adminRevoked");

    /// <summary>
    /// Gets the security alert line for an authenticator application disabled.
    /// </summary>
    public static string EmailsSecurityAlertAuthenticatorDisabled =>
        Get("emails.securityAlert.authenticatorDisabled");

    /// <summary>
    /// Gets the security alert line for an authenticator application enabled.
    /// </summary>
    public static string EmailsSecurityAlertAuthenticatorEnabled =>
        Get("emails.securityAlert.authenticatorEnabled");

    /// <summary>
    /// Gets the heading of the security alert email.
    /// </summary>
    public static string EmailsSecurityAlertHeading => Get("emails.securityAlert.heading");

    /// <summary>
    /// Gets the security alert line for a changed email, sent to the previous address.
    /// </summary>
    public static string EmailsSecurityAlertEmailChanged =>
        Get("emails.securityAlert.emailChanged");

    /// <summary>
    /// Gets the security alert line for a changed contact phone.
    /// </summary>
    public static string EmailsSecurityAlertPhoneChanged =>
        Get("emails.securityAlert.phoneChanged");

    /// <summary>
    /// Gets the security alert line for a changed email and contact phone, sent to the previous
    /// address.
    /// </summary>
    public static string EmailsSecurityAlertEmailAndPhoneChanged =>
        Get("emails.securityAlert.emailAndPhoneChanged");

    /// <summary>
    /// Gets the HTML introduction of the security alert email.
    /// </summary>
    public static string EmailsSecurityAlertIntroHtml => Get("emails.securityAlert.introHtml");

    /// <summary>
    /// Gets the plain-text introduction of the security alert email.
    /// </summary>
    public static string EmailsSecurityAlertIntroText => Get("emails.securityAlert.introText");

    /// <summary>
    /// Gets the advice for an email change the owner did not make, which a password reset can no
    /// longer undo because the reset link would reach the new address.
    /// </summary>
    public static string EmailsSecurityAlertEmailChangedWarning =>
        Get("emails.securityAlert.emailChangedWarning");

    /// <summary>
    /// Gets the advice for a change an administrator made on the account: a second-factor reset or
    /// administrator rights granted or revoked.
    /// </summary>
    public static string EmailsSecurityAlertUnexpectedWarning =>
        Get("emails.securityAlert.unexpectedWarning");

    /// <summary>
    /// Gets the advice for a password lock caused by someone else's attempts.
    /// </summary>
    public static string EmailsSecurityAlertPasswordLockedWarning =>
        Get("emails.securityAlert.passwordLockedWarning");

    /// <summary>
    /// Gets the security alert line for a changed password.
    /// </summary>
    public static string EmailsSecurityAlertPasswordChanged =>
        Get("emails.securityAlert.passwordChanged");

    /// <summary>
    /// Gets the security alert line for an account locked after repeated wrong passwords.
    /// </summary>
    public static string EmailsSecurityAlertPasswordLocked =>
        Get("emails.securityAlert.passwordLocked");

    /// <summary>
    /// Gets the security alert line for a password reset through a forgotten password.
    /// </summary>
    public static string EmailsSecurityAlertPasswordReset =>
        Get("emails.securityAlert.passwordReset");

    /// <summary>
    /// Gets the subject of the security alert email.
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
    /// Gets the security alert line for a second factor an administrator reset.
    /// </summary>
    public static string EmailsSecurityAlertTwoFactorReset =>
        Get("emails.securityAlert.twoFactorReset");

    /// <summary>
    /// Gets the advice for any other change the owner did not make: reset the password and contact
    /// the association.
    /// </summary>
    public static string EmailsSecurityAlertWarning => Get("emails.securityAlert.warning");

    /// <summary>
    /// Gets the note that offers the link as text when the email button does not work.
    /// </summary>
    public static string EmailsSharedFallbackLinkNote => Get("emails.shared.fallbackLinkNote");

    /// <summary>
    /// Gets the alternative text of the logo in emails.
    /// </summary>
    public static string EmailsSharedLogoAlt => Get("emails.shared.logoAlt");

    /// <summary>
    /// Gets the HTML sign-off of emails.
    /// </summary>
    public static string EmailsSharedSignoffHtml => Get("emails.shared.signoffHtml");

    /// <summary>
    /// Gets the plain-text sign-off of emails.
    /// </summary>
    public static string EmailsSharedSignoffText => Get("emails.shared.signoffText");

    /// <summary>
    /// Gets the button label of the email that verifies a new account.
    /// </summary>
    public static string EmailsVerificationButtonLabel => Get("emails.verification.buttonLabel");

    /// <summary>
    /// Gets the heading of the account verification email.
    /// </summary>
    public static string EmailsVerificationHeading => Get("emails.verification.heading");

    /// <summary>
    /// Gets the closing note of the account verification email for whoever did not register.
    /// </summary>
    public static string EmailsVerificationIgnoreNote => Get("emails.verification.ignoreNote");

    /// <summary>
    /// Gets the paragraph that reminds the news consent given at registration.
    /// </summary>
    public static string EmailsVerificationNewsConsent => Get("emails.verification.newsConsent");

    /// <summary>
    /// Gets the HTML introduction of the account verification email.
    /// </summary>
    public static string EmailsVerificationIntroHtml => Get("emails.verification.introHtml");

    /// <summary>
    /// Gets the plain-text introduction of the account verification email.
    /// </summary>
    public static string EmailsVerificationIntroText => Get("emails.verification.introText");

    /// <summary>
    /// Gets the subject of the account verification email.
    /// </summary>
    public static string EmailsVerificationSubject => Get("emails.verification.subject");

    /// <summary>
    /// Gets the name given to a manual email attachment whose name is empty once sanitized.
    /// </summary>
    public static string FilesFallbackAttachmentName => Get("files.fallbackAttachmentName");

    /// <summary>
    /// Gets the name given to an uploaded file whose name is empty once sanitized.
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
    /// Formats the HTML lifetime of the code that confirms deleting one's account.
    /// </summary>
    /// <param name="minutes">Minutes the code stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsAccountDeletionCodeExpiryHtml(int minutes)
    {
        return Format("emails.accountDeletionCode.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the plain-text lifetime of the code that confirms deleting one's account.
    /// </summary>
    /// <param name="minutes">Minutes the code stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsAccountDeletionCodeExpiryText(int minutes)
    {
        return Format("emails.accountDeletionCode.expiryText", minutes);
    }

    /// <summary>
    /// Formats the introduction of the email that confirms an activity signup.
    /// </summary>
    /// <param name="signupPhrase">
    /// How the signup is named, from <see cref="EmailsActivityDecisionSignupPhraseSelf"/> or
    /// <see cref="EmailsActivityDecisionSignupPhraseNamed"/>.
    /// </param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionConfirmedIntro(string signupPhrase)
    {
        return Format("emails.activityDecision.confirmedIntro", signupPhrase);
    }

    /// <summary>
    /// Formats the subject of the email that confirms an activity signup.
    /// </summary>
    /// <param name="activityTitle">Title of the activity.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionConfirmedSubject(string activityTitle)
    {
        return Format("emails.activityDecision.confirmedSubject", activityTitle);
    }

    /// <summary>
    /// Formats the introduction of the email that denies an activity signup.
    /// </summary>
    /// <param name="signupPhrase">
    /// How the signup is named, from <see cref="EmailsActivityDecisionSignupPhraseSelf"/> or
    /// <see cref="EmailsActivityDecisionSignupPhraseNamed"/>.
    /// </param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionDeniedIntro(string signupPhrase)
    {
        return Format("emails.activityDecision.deniedIntro", signupPhrase);
    }

    /// <summary>
    /// Formats the subject of the email that denies an activity signup.
    /// </summary>
    /// <param name="activityTitle">Title of the activity.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionDeniedSubject(string activityTitle)
    {
        return Format("emails.activityDecision.deniedSubject", activityTitle);
    }

    /// <summary>
    /// Formats how an activity decision email to a guardian names the signup of their dependent.
    /// </summary>
    /// <param name="participantName">Name of the dependent signed up.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsActivityDecisionSignupPhraseNamed(string participantName)
    {
        return Format("emails.activityDecision.signupPhraseNamed", participantName);
    }

    /// <summary>
    /// Formats the HTML introduction of the email that tells a guardian a dependent comes of age.
    /// </summary>
    /// <param name="dependentName">Name of the dependent, already HTML-encoded.</param>
    /// <param name="comingOfAgeOn">Day the dependent comes of age.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsComingOfAgeIntroHtml(string dependentName, string comingOfAgeOn)
    {
        return Format("emails.comingOfAge.introHtml", dependentName, comingOfAgeOn);
    }

    /// <summary>
    /// Formats the plain-text introduction of the email that tells a guardian a dependent comes of
    /// age.
    /// </summary>
    /// <param name="dependentName">Name of the dependent.</param>
    /// <param name="comingOfAgeOn">Day the dependent comes of age.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsComingOfAgeIntroText(string dependentName, string comingOfAgeOn)
    {
        return Format("emails.comingOfAge.introText", dependentName, comingOfAgeOn);
    }

    /// <summary>
    /// Formats the HTML introduction of the notice of a withdrawn consent.
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
    /// Formats the subject of the notice of several consultations of the blocked data.
    /// </summary>
    /// <param name="count">Number of consultations.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsBlockedDataConsultationSubjectMany(int count)
    {
        return Format("emails.blockedDataConsultation.subjectMany", count);
    }

    /// <summary>
    /// Formats the subject of the notice of several withdrawn consents.
    /// </summary>
    /// <param name="count">Number of withdrawn consents.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsConsentWithdrawnSubjectMany(int count)
    {
        return Format("emails.consentWithdrawn.subjectMany", count);
    }

    /// <summary>
    /// Formats the HTML lifetime of the code that confirms opening a consultation.
    /// </summary>
    /// <param name="minutes">Minutes the code stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsBlockedDataConsultationCodeExpiryHtml(int minutes)
    {
        return Format("emails.blockedDataConsultationCode.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the plain-text lifetime of the code that confirms opening a consultation.
    /// </summary>
    /// <param name="minutes">Minutes the code stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsBlockedDataConsultationCodeExpiryText(int minutes)
    {
        return Format("emails.blockedDataConsultationCode.expiryText", minutes);
    }

    /// <summary>
    /// Formats the plain-text introduction of the notice of a withdrawn consent.
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
    /// Formats who recorded a withdrawn consent and when, for its notice.
    /// </summary>
    /// <param name="recordedByName">Name of the account that recorded the withdrawal.</param>
    /// <param name="withdrawnAt">When it was withdrawn, already formatted.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsConsentWithdrawnRecorded(string recordedByName, string withdrawnAt)
    {
        return Format("emails.consentWithdrawn.recorded", recordedByName, withdrawnAt);
    }

    /// <summary>
    /// Formats a row of the details block of a plain-text email.
    /// </summary>
    /// <param name="label">Label of the row, such as <see cref="EmailsDetailsEventLabel"/>.</param>
    /// <param name="value">Value shown after the label.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsDetailsRowText(string label, string value)
    {
        return Format("emails.details.rowText", label, value);
    }

    /// <summary>
    /// Formats a schedule that ends on a later day than it starts.
    /// </summary>
    /// <param name="startDate">Day it starts, already formatted.</param>
    /// <param name="startTime">Time it starts, already formatted.</param>
    /// <param name="endDate">Day it ends, already formatted.</param>
    /// <param name="endTime">Time it ends, already formatted.</param>
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
    /// Formats a schedule that starts and ends on the same day.
    /// </summary>
    /// <param name="startDate">Day it takes place, already formatted.</param>
    /// <param name="startTime">Time it starts, already formatted.</param>
    /// <param name="endTime">Time it ends, already formatted.</param>
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
    /// Formats the HTML lifetime of the link that confirms a new email address.
    /// </summary>
    /// <param name="minutes">Minutes the link stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsEmailChangeExpiryHtml(int minutes)
    {
        return Format("emails.emailChange.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the plain-text lifetime of the link that confirms a new email address.
    /// </summary>
    /// <param name="minutes">Minutes the link stays valid.</param>
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
    /// Formats the HTML lifetime of a login code.
    /// </summary>
    /// <param name="minutes">Minutes the code stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsLoginCodeExpiryHtml(int minutes)
    {
        return Format("emails.loginCode.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the plain-text lifetime of a login code.
    /// </summary>
    /// <param name="minutes">Minutes the code stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsLoginCodeExpiryText(int minutes)
    {
        return Format("emails.loginCode.expiryText", minutes);
    }

    /// <summary>
    /// Formats the HTML lifetime of a password reset link.
    /// </summary>
    /// <param name="minutes">Minutes the link stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsPasswordResetExpiryHtml(int minutes)
    {
        return Format("emails.passwordReset.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the plain-text lifetime of a password reset link.
    /// </summary>
    /// <param name="minutes">Minutes the link stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsPasswordResetExpiryText(int minutes)
    {
        return Format("emails.passwordReset.expiryText", minutes);
    }

    /// <summary>
    /// Formats the security alert line that hints at the new email of the account.
    /// </summary>
    /// <param name="maskedEmail">New email with most of it masked.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsSecurityAlertNewEmail(string maskedEmail)
    {
        return Format("emails.securityAlert.newEmail", maskedEmail);
    }

    /// <summary>
    /// Formats when the change a security alert reports happened.
    /// </summary>
    /// <param name="occurredAt">When it happened, already formatted.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsSecurityAlertWhen(string occurredAt)
    {
        return Format("emails.securityAlert.when", occurredAt);
    }

    /// <summary>
    /// Formats the greeting that opens an email.
    /// </summary>
    /// <param name="name">Name of the recipient.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsSharedGreeting(string name)
    {
        return Format("emails.shared.greeting", name);
    }

    /// <summary>
    /// Formats the HTML lifetime of the link that verifies a new account.
    /// </summary>
    /// <param name="minutes">Minutes the link stays valid.</param>
    /// <returns>The generated text.</returns>
    public static string EmailsVerificationExpiryHtml(int minutes)
    {
        return Format("emails.verification.expiryHtml", minutes);
    }

    /// <summary>
    /// Formats the plain-text lifetime of the link that verifies a new account.
    /// </summary>
    /// <param name="minutes">Minutes the link stays valid.</param>
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
