using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the email that tells a participant, or their guardian, how an activity signup was decided.
/// </summary>
public static class ActivitySignupDecisionEmail
{
    /// <summary>
    /// Builds the email sent when an activity signup is confirmed: the details with the role, a reminder
    /// to cancel the signup when the participant cannot attend and a button to the event page. The
    /// subject names the activity.
    /// </summary>
    /// <param name="toAddress">Address of the participant or guardian.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="participantName">
    /// Name of the dependent the signup is for, when writing to a guardian; null or blank when the signup
    /// is the recipient's own.
    /// </param>
    /// <param name="roleName">Role the participant was confirmed in; null or blank leaves the row out.</param>
    /// <param name="details">Activity the signup is for.</param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <returns>The message, of kind <see cref="EmailKind.ActivityNotification"/>.</returns>
    public static EmailMessage Confirmed(
        string toAddress,
        string toName,
        string? participantName,
        string? roleName,
        ActivityEmailDetails details,
        TimeZoneInfo timeZone,
        string siteUrl
    )
    {
        return Create(
            toAddress,
            toName,
            details,
            timeZone,
            siteUrl,
            new DecisionContent(
                AppStrings.EmailsActivityDecisionConfirmedHeading,
                AppStrings.EmailsActivityDecisionConfirmedSubject(details.ActivityTitle),
                AppStrings.EmailsActivityDecisionConfirmedIntro(SignupPhrase(participantName)),
                AppStrings.EmailsActivityDecisionConfirmedIntro(
                    SignupPhrase(WebUtility.HtmlEncode(participantName))
                ),
                AppStrings.EmailsActivityDecisionConfirmedNote,
                AppStrings.EmailsActivityDecisionConfirmedButtonLabel,
                details.EventUrl,
                roleName,
                EmailBranding.Success
            )
        );
    }

    /// <summary>
    /// Builds the email sent when an activity signup is denied: the details without a role and a button
    /// to the events list. The subject names the activity.
    /// </summary>
    /// <param name="toAddress">Address of the participant or guardian.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="participantName">
    /// Name of the dependent the signup is for, when writing to a guardian; null or blank when the signup
    /// is the recipient's own.
    /// </param>
    /// <param name="details">Activity the signup is for.</param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <param name="eventsUrl">Events list where the recipient can look for another one.</param>
    /// <returns>The message, of kind <see cref="EmailKind.ActivityNotification"/>.</returns>
    public static EmailMessage Denied(
        string toAddress,
        string toName,
        string? participantName,
        ActivityEmailDetails details,
        TimeZoneInfo timeZone,
        string siteUrl,
        string eventsUrl
    )
    {
        return Create(
            toAddress,
            toName,
            details,
            timeZone,
            siteUrl,
            new DecisionContent(
                AppStrings.EmailsActivityDecisionDeniedHeading,
                AppStrings.EmailsActivityDecisionDeniedSubject(details.ActivityTitle),
                AppStrings.EmailsActivityDecisionDeniedIntro(SignupPhrase(participantName)),
                AppStrings.EmailsActivityDecisionDeniedIntro(
                    SignupPhrase(WebUtility.HtmlEncode(participantName))
                ),
                null,
                AppStrings.EmailsActivityDecisionDeniedButtonLabel,
                eventsUrl,
                null,
                EmailBranding.Danger
            )
        );
    }

    private static EmailMessage Create(
        string toAddress,
        string toName,
        ActivityEmailDetails details,
        TimeZoneInfo timeZone,
        string siteUrl,
        DecisionContent content
    )
    {
        var blocks = new List<EmailBlock>
        {
            EmailBlocks.Prose(content.IntroHtml, content.IntroText),
            details.ToBlock(timeZone, content.RoleName),
        };

        if (content.Note is not null)
        {
            blocks.Add(EmailBlocks.Callout(content.Note, content.Accent));
        }

        blocks.Add(EmailBlocks.Action(content.ButtonLabel, content.ButtonUrl));
        blocks.Add(EmailBlocks.Signoff());

        var rendered = EmailLayout.Render(
            new EmailDocument(content.Heading, content.IntroText, toName, siteUrl, content.Accent),
            blocks
        );

        return rendered.ToMessage(
            EmailKind.ActivityNotification,
            toAddress,
            toName,
            content.Subject
        );
    }

    private static string SignupPhrase(string? participantName)
    {
        return string.IsNullOrWhiteSpace(participantName)
            ? AppStrings.EmailsActivityDecisionSignupPhraseSelf
            : AppStrings.EmailsActivityDecisionSignupPhraseNamed(participantName);
    }

    private sealed record DecisionContent(
        string Heading,
        string Subject,
        string IntroText,
        string IntroHtml,
        string? Note,
        string ButtonLabel,
        string ButtonUrl,
        string? RoleName,
        EmailAccent Accent
    );
}
