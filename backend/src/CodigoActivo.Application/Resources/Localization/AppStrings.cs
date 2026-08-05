using System.Globalization;
using System.Resources;

namespace CodigoActivo.Application.Resources.Localization;

public static class AppStrings
{
    public const string BaseName =
        "CodigoActivo.Application.Resources.Localization.AppStrings";
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly ResourceManager Manager = new(BaseName, typeof(AppStrings).Assembly);

    public static string EmailsActivityDecisionConfirmedButtonLabel => Get("emails.activityDecision.confirmedButtonLabel");

    public static string EmailsActivityDecisionConfirmedHeading => Get("emails.activityDecision.confirmedHeading");

    public static string EmailsActivityDecisionConfirmedNote => Get("emails.activityDecision.confirmedNote");

    public static string EmailsActivityDecisionDeniedButtonLabel => Get("emails.activityDecision.deniedButtonLabel");

    public static string EmailsActivityDecisionDeniedHeading => Get("emails.activityDecision.deniedHeading");

    public static string EmailsActivityDecisionSignupPhraseSelf => Get("emails.activityDecision.signupPhraseSelf");

    public static string EmailsActivitySignupAccountPrompt => Get("emails.activitySignup.accountPrompt");

    public static string EmailsActivitySignupButtonLabel => Get("emails.activitySignup.buttonLabel");

    public static string EmailsActivitySignupHeading => Get("emails.activitySignup.heading");

    public static string EmailsActivitySignupIntro => Get("emails.activitySignup.intro");

    public static string EmailsActivitySignupParticipantsLabel => Get("emails.activitySignup.participantsLabel");

    public static string EmailsActivitySignupPending => Get("emails.activitySignup.pending");

    public static string EmailsDetailsActivityLabel => Get("emails.details.activityLabel");

    public static string EmailsDetailsEventLabel => Get("emails.details.eventLabel");

    public static string EmailsDetailsLocationLabel => Get("emails.details.locationLabel");

    public static string EmailsDetailsRoleLabel => Get("emails.details.roleLabel");

    public static string EmailsDetailsScheduleLabel => Get("emails.details.scheduleLabel");

    public static string EmailsFooterAutomaticNote => Get("emails.footer.automaticNote");

    public static string EmailsFooterTagline => Get("emails.footer.tagline");

    public static string EmailsFooterWebsiteLabel => Get("emails.footer.websiteLabel");

    public static string EmailsManualSignature => Get("emails.manual.signature");

    public static string EmailsPasswordResetButtonLabel => Get("emails.passwordReset.buttonLabel");

    public static string EmailsPasswordResetHeading => Get("emails.passwordReset.heading");

    public static string EmailsPasswordResetIgnoreNote => Get("emails.passwordReset.ignoreNote");

    public static string EmailsPasswordResetIntroHtml => Get("emails.passwordReset.introHtml");

    public static string EmailsPasswordResetIntroText => Get("emails.passwordReset.introText");

    public static string EmailsPasswordResetSubject => Get("emails.passwordReset.subject");

    public static string EmailsSharedBrandName => Get("emails.shared.brandName");

    public static string EmailsSharedFallbackLinkNote => Get("emails.shared.fallbackLinkNote");

    public static string EmailsSharedLogoAlt => Get("emails.shared.logoAlt");

    public static string EmailsSharedSignoffHtml => Get("emails.shared.signoffHtml");

    public static string EmailsSharedSignoffText => Get("emails.shared.signoffText");

    public static string EmailsVerificationButtonLabel => Get("emails.verification.buttonLabel");

    public static string EmailsVerificationCodePrompt => Get("emails.verification.codePrompt");

    public static string EmailsVerificationHeading => Get("emails.verification.heading");

    public static string EmailsVerificationIgnoreNote => Get("emails.verification.ignoreNote");

    public static string EmailsVerificationIntroHtml => Get("emails.verification.introHtml");

    public static string EmailsVerificationIntroText => Get("emails.verification.introText");

    public static string EmailsVerificationSubject => Get("emails.verification.subject");

    public static string FilesFallbackAttachmentName => Get("files.fallbackAttachmentName");

    public static string FilesFallbackFileName => Get("files.fallbackFileName");

    public static string EmailsActivityDecisionConfirmedIntro(string signupPhrase)
    {
        return Format("emails.activityDecision.confirmedIntro", signupPhrase);
    }

    public static string EmailsActivityDecisionConfirmedSubject(string activityTitle)
    {
        return Format("emails.activityDecision.confirmedSubject", activityTitle);
    }

    public static string EmailsActivityDecisionDeniedIntro(string signupPhrase)
    {
        return Format("emails.activityDecision.deniedIntro", signupPhrase);
    }

    public static string EmailsActivityDecisionDeniedSubject(string activityTitle)
    {
        return Format("emails.activityDecision.deniedSubject", activityTitle);
    }

    public static string EmailsActivityDecisionSignupPhraseNamed(string participantName)
    {
        return Format("emails.activityDecision.signupPhraseNamed", participantName);
    }

    public static string EmailsActivitySignupParticipantHtml(string fullName, string roleName)
    {
        return Format("emails.activitySignup.participantHtml", fullName, roleName);
    }

    public static string EmailsActivitySignupParticipantText(string fullName, string roleName)
    {
        return Format("emails.activitySignup.participantText", fullName, roleName);
    }

    public static string EmailsActivitySignupSubject(string activityTitle)
    {
        return Format("emails.activitySignup.subject", activityTitle);
    }

    public static string EmailsDetailsRowText(string label, string value)
    {
        return Format("emails.details.rowText", label, value);
    }

    public static string EmailsDetailsScheduleMultiDay(
        string startDate,
        string startTime,
        string endDate,
        string endTime
    )
    {
        return Format("emails.details.scheduleMultiDay", startDate, startTime, endDate, endTime);
    }

    public static string EmailsDetailsScheduleSameDay(
        string startDate,
        string startTime,
        string endTime
    )
    {
        return Format("emails.details.scheduleSameDay", startDate, startTime, endTime);
    }

    public static string EmailsPasswordResetExpiryHtml(int minutes)
    {
        return Format("emails.passwordReset.expiryHtml", minutes);
    }

    public static string EmailsPasswordResetExpiryText(int minutes)
    {
        return Format("emails.passwordReset.expiryText", minutes);
    }

    public static string EmailsSharedGreeting(string name)
    {
        return Format("emails.shared.greeting", name);
    }

    public static string EmailsVerificationExpiryHtml(int minutes)
    {
        return Format("emails.verification.expiryHtml", minutes);
    }

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
