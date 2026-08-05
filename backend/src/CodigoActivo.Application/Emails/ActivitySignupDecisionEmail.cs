using System.Net;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public static class ActivitySignupDecisionEmail
{
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
            EmailBlocks.Prose(AppStrings.EmailsSharedSignoffHtml, AppStrings.EmailsSharedSignoffText)
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
