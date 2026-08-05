using System.Net;
using CodigoActivo.Application.Localization;
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
        TimeZoneInfo timeZone
    )
    {
        return Create(
            toAddress,
            toName,
            details,
            timeZone,
            new DecisionContent(
                AppStrings.EmailsActivityDecisionConfirmedHeading,
                AppStrings.EmailsActivityDecisionConfirmedSubject(details.ActivityTitle),
                AppStrings.EmailsActivityDecisionConfirmedIntro(SignupPhrase(participantName)),
                AppStrings.EmailsActivityDecisionConfirmedIntro(
                    SignupPhrase(WebUtility.HtmlEncode(participantName))
                ),
                AppStrings.EmailsActivityDecisionConfirmedNote,
                AppStrings.EmailsActivityDecisionConfirmedButtonLabel,
                roleName
            )
        );
    }

    public static EmailMessage Denied(
        string toAddress,
        string toName,
        string? participantName,
        ActivityEmailDetails details,
        TimeZoneInfo timeZone
    )
    {
        return Create(
            toAddress,
            toName,
            details,
            timeZone,
            new DecisionContent(
                AppStrings.EmailsActivityDecisionDeniedHeading,
                AppStrings.EmailsActivityDecisionDeniedSubject(details.ActivityTitle),
                AppStrings.EmailsActivityDecisionDeniedIntro(SignupPhrase(participantName)),
                AppStrings.EmailsActivityDecisionDeniedIntro(
                    SignupPhrase(WebUtility.HtmlEncode(participantName))
                ),
                null,
                AppStrings.EmailsActivityDecisionDeniedButtonLabel,
                null
            )
        );
    }

    private static EmailMessage Create(
        string toAddress,
        string toName,
        ActivityEmailDetails details,
        TimeZoneInfo timeZone,
        DecisionContent content
    )
    {
        var encodedName = WebUtility.HtmlEncode(toName);
        var encodedUrl = WebUtility.HtmlEncode(details.EventUrl);
        var noteText = content.Note is null ? string.Empty : $"{content.Note}\n\n";
        var noteHtml = content.Note is null ? string.Empty : $"<p>{content.Note}</p>";

        var textBody = $"""
            {AppStrings.EmailsSharedGreeting(toName)}

            {content.IntroText}

            {details.ToTextBlock(timeZone, content.RoleName)}

            {noteText}{details.EventUrl}

            {AppStrings.EmailsSharedSignoffText}
            """;

        var htmlBody = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;">
              <h2 style="color: #111827;">{content.Heading}</h2>
              <p>{AppStrings.EmailsSharedGreeting(encodedName)}</p>
              <p>{content.IntroHtml}</p>
              {details.ToHtmlBlock(timeZone, content.RoleName)}
              {noteHtml}
              <p style="text-align: center; margin: 24px 0;">
                <a href="{encodedUrl}" style="display: inline-block; padding: 12px 28px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold;">{content.ButtonLabel}</a>
              </p>
              <p style="color: #6b7280; font-size: 13px;">{AppStrings.EmailsSharedFallbackLinkNote}<br>{encodedUrl}</p>
              <p style="color: #6b7280; font-size: 13px;">{AppStrings.EmailsSharedSignoffHtml}</p>
            </div>
            """;

        return new EmailMessage(
            EmailKind.ActivityNotification,
            toAddress,
            toName,
            content.Subject,
            htmlBody,
            textBody
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
        string? RoleName
    );
}
