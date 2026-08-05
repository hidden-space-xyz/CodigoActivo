using System.Net;
using CodigoActivo.Application.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public sealed record ActivitySignupParticipant(string FullName, string RoleName);

public static class ActivitySignupEmail
{
    public static EmailMessage Create(
        string toAddress,
        string toName,
        ActivityEmailDetails details,
        IReadOnlyList<ActivitySignupParticipant> participants,
        TimeZoneInfo timeZone,
        string accountUrl
    )
    {
        var encodedName = WebUtility.HtmlEncode(toName);
        var encodedUrl = WebUtility.HtmlEncode(accountUrl);

        var participantsText = string.Join(
            "\n",
            participants.Select(participant =>
                AppStrings.EmailsActivitySignupParticipantText(
                    participant.FullName,
                    participant.RoleName
                )
            )
        );

        var participantsHtml = string.Concat(
            participants.Select(participant =>
                "<li style=\"margin-bottom: 4px;\">"
                + AppStrings.EmailsActivitySignupParticipantHtml(
                    WebUtility.HtmlEncode(participant.FullName),
                    WebUtility.HtmlEncode(participant.RoleName)
                )
                + "</li>"
            )
        );

        var textBody = $"""
            {AppStrings.EmailsSharedGreeting(toName)}

            {AppStrings.EmailsActivitySignupIntro}

            {details.ToTextBlock(timeZone)}

            {AppStrings.EmailsActivitySignupParticipantsLabel}
            {participantsText}

            {AppStrings.EmailsActivitySignupPending}

            {AppStrings.EmailsActivitySignupAccountPrompt}

            {accountUrl}

            {AppStrings.EmailsSharedSignoffText}
            """;

        var htmlBody = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;">
              <h2 style="color: #111827;">{AppStrings.EmailsActivitySignupHeading}</h2>
              <p>{AppStrings.EmailsSharedGreeting(encodedName)}</p>
              <p>{AppStrings.EmailsActivitySignupIntro}</p>
              {details.ToHtmlBlock(timeZone)}
              <p style="margin-bottom: 4px;">{AppStrings.EmailsActivitySignupParticipantsLabel}</p>
              <ul style="margin-top: 0; padding-left: 20px;">{participantsHtml}</ul>
              <p>{AppStrings.EmailsActivitySignupPending}</p>
              <p style="text-align: center; margin: 24px 0;">
                <a href="{encodedUrl}" style="display: inline-block; padding: 12px 28px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: bold;">{AppStrings.EmailsActivitySignupButtonLabel}</a>
              </p>
              <p style="color: #6b7280; font-size: 13px;">{AppStrings.EmailsSharedFallbackLinkNote}<br>{encodedUrl}</p>
              <p style="color: #6b7280; font-size: 13px;">{AppStrings.EmailsSharedSignoffHtml}</p>
            </div>
            """;

        return new EmailMessage(
            EmailKind.ActivityNotification,
            toAddress,
            toName,
            AppStrings.EmailsActivitySignupSubject(details.ActivityTitle),
            htmlBody,
            textBody
        );
    }
}
