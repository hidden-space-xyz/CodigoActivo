using System.Globalization;
using System.Resources;

namespace CodigoActivo.Application.Resources.Localization;

/// <summary>
/// Provides strongly typed access to localized application strings.
/// </summary>
public static class AppStrings
{
    /// <summary>
    /// Identifies the base name configuration or policy value.
    /// </summary>
    public const string BaseName =
        "CodigoActivo.Application.Resources.Localization.AppStrings";
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly ResourceManager Manager = new(BaseName, typeof(AppStrings).Assembly);

    /// <summary>
    /// Gets the emails account deletion code heading value.
    /// </summary>
    public static string EmailsAccountDeletionCodeHeading => Get("emails.accountDeletionCode.heading");

    /// <summary>
    /// Gets the emails account deletion code ignore note value.
    /// </summary>
    public static string EmailsAccountDeletionCodeIgnoreNote => Get("emails.accountDeletionCode.ignoreNote");

    /// <summary>
    /// Gets the emails account deletion code intro html value.
    /// </summary>
    public static string EmailsAccountDeletionCodeIntroHtml => Get("emails.accountDeletionCode.introHtml");

    /// <summary>
    /// Gets the emails account deletion code intro text value.
    /// </summary>
    public static string EmailsAccountDeletionCodeIntroText => Get("emails.accountDeletionCode.introText");

    /// <summary>
    /// Gets the emails account deletion code subject value.
    /// </summary>
    public static string EmailsAccountDeletionCodeSubject => Get("emails.accountDeletionCode.subject");

    /// <summary>
    /// Gets the emails activity decision confirmed button label value.
    /// </summary>
    public static string EmailsActivityDecisionConfirmedButtonLabel => Get("emails.activityDecision.confirmedButtonLabel");

    /// <summary>
    /// Gets the emails activity decision confirmed heading value.
    /// </summary>
    public static string EmailsActivityDecisionConfirmedHeading => Get("emails.activityDecision.confirmedHeading");

    /// <summary>
    /// Gets the emails activity decision confirmed note value.
    /// </summary>
    public static string EmailsActivityDecisionConfirmedNote => Get("emails.activityDecision.confirmedNote");

    /// <summary>
    /// Gets the emails activity decision denied button label value.
    /// </summary>
    public static string EmailsActivityDecisionDeniedButtonLabel => Get("emails.activityDecision.deniedButtonLabel");

    /// <summary>
    /// Gets the emails activity decision denied heading value.
    /// </summary>
    public static string EmailsActivityDecisionDeniedHeading => Get("emails.activityDecision.deniedHeading");

    /// <summary>
    /// Gets the emails activity decision signup phrase self value.
    /// </summary>
    public static string EmailsActivityDecisionSignupPhraseSelf => Get("emails.activityDecision.signupPhraseSelf");

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
    /// Gets the emails footer automatic note value.
    /// </summary>
    public static string EmailsFooterAutomaticNote => Get("emails.footer.automaticNote");

    /// <summary>
    /// Gets the emails footer tagline value.
    /// </summary>
    public static string EmailsFooterTagline => Get("emails.footer.tagline");

    /// <summary>
    /// Gets the emails footer website label value.
    /// </summary>
    public static string EmailsFooterWebsiteLabel => Get("emails.footer.websiteLabel");

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
    /// Gets the emails manual signature value.
    /// </summary>
    public static string EmailsManualSignature => Get("emails.manual.signature");

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
    public static string EmailsSecurityAlertAdminGranted => Get("emails.securityAlert.adminGranted");

    /// <summary>
    /// Gets the emails security alert admin revoked value.
    /// </summary>
    public static string EmailsSecurityAlertAdminRevoked => Get("emails.securityAlert.adminRevoked");

    /// <summary>
    /// Gets the emails security alert authenticator disabled value.
    /// </summary>
    public static string EmailsSecurityAlertAuthenticatorDisabled => Get("emails.securityAlert.authenticatorDisabled");

    /// <summary>
    /// Gets the emails security alert authenticator enabled value.
    /// </summary>
    public static string EmailsSecurityAlertAuthenticatorEnabled => Get("emails.securityAlert.authenticatorEnabled");

    /// <summary>
    /// Gets the emails security alert heading value.
    /// </summary>
    public static string EmailsSecurityAlertHeading => Get("emails.securityAlert.heading");

    /// <summary>
    /// Gets the emails security alert identifiers changed value.
    /// </summary>
    public static string EmailsSecurityAlertIdentifiersChanged => Get("emails.securityAlert.identifiersChanged");

    /// <summary>
    /// Gets the emails security alert intro html value.
    /// </summary>
    public static string EmailsSecurityAlertIntroHtml => Get("emails.securityAlert.introHtml");

    /// <summary>
    /// Gets the emails security alert intro text value.
    /// </summary>
    public static string EmailsSecurityAlertIntroText => Get("emails.securityAlert.introText");

    /// <summary>
    /// Gets the emails security alert no action note value.
    /// </summary>
    public static string EmailsSecurityAlertNoActionNote => Get("emails.securityAlert.noActionNote");

    /// <summary>
    /// Gets the emails security alert password changed value.
    /// </summary>
    public static string EmailsSecurityAlertPasswordChanged => Get("emails.securityAlert.passwordChanged");

    /// <summary>
    /// Gets the emails security alert password reset value.
    /// </summary>
    public static string EmailsSecurityAlertPasswordReset => Get("emails.securityAlert.passwordReset");

    /// <summary>
    /// Gets the emails security alert subject value.
    /// </summary>
    public static string EmailsSecurityAlertSubject => Get("emails.securityAlert.subject");

    /// <summary>
    /// Gets the emails security alert two factor reset value.
    /// </summary>
    public static string EmailsSecurityAlertTwoFactorReset => Get("emails.securityAlert.twoFactorReset");

    /// <summary>
    /// Gets the emails security alert warning value.
    /// </summary>
    public static string EmailsSecurityAlertWarning => Get("emails.securityAlert.warning");

    /// <summary>
    /// Gets the emails shared brand name value.
    /// </summary>
    public static string EmailsSharedBrandName => Get("emails.shared.brandName");

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
