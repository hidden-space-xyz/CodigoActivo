using System.Net;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Builds the email content for activity signup decision.
/// </summary>
public static class ActivitySignupDecisionEmail
{
    /// <summary>
    /// Builds the email sent when an activity signup is confirmed.
    /// </summary>
    /// <param name="toAddress">The to address value.</param>
    /// <param name="toName">The to name value.</param>
    /// <param name="participantName">The participant name value.</param>
    /// <param name="roleName">The role name value.</param>
    /// <param name="details">The details value.</param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <returns>The resulting email message value.</returns>
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
                roleName,
                EmailBranding.Success
            )
        );
    }

    /// <summary>
    /// Builds the email sent when an activity signup is denied.
    /// </summary>
    /// <param name="toAddress">The to address value.</param>
    /// <param name="toName">The to name value.</param>
    /// <param name="participantName">The participant name value.</param>
    /// <param name="details">The details value.</param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <returns>The resulting email message value.</returns>
    public static EmailMessage Denied(
        string toAddress,
        string toName,
        string? participantName,
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
                AppStrings.EmailsActivityDecisionDeniedHeading,
                AppStrings.EmailsActivityDecisionDeniedSubject(details.ActivityTitle),
                AppStrings.EmailsActivityDecisionDeniedIntro(SignupPhrase(participantName)),
                AppStrings.EmailsActivityDecisionDeniedIntro(
                    SignupPhrase(WebUtility.HtmlEncode(participantName))
                ),
                null,
                AppStrings.EmailsActivityDecisionDeniedButtonLabel,
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

        blocks.Add(EmailBlocks.Action(content.ButtonLabel, details.EventUrl));
        blocks.Add(
            EmailBlocks.Prose(
                AppStrings.EmailsSharedSignoffHtml,
                AppStrings.EmailsSharedSignoffText
            )
        );

        var rendered = EmailLayout.Render(
            new EmailDocument(
                content.Heading,
                content.IntroText,
                toName,
                siteUrl,
                content.Accent,
                AppStrings.EmailsFooterAutomaticNote
            ),
            blocks
        );

        return new EmailMessage(
            EmailKind.ActivityNotification,
            toAddress,
            toName,
            content.Subject,
            rendered.Html,
            rendered.Text,
            InlineImages: rendered.InlineImages
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
        string? RoleName,
        EmailAccent Accent
    );
}
