using System.Net;
using CodigoActivo.Application.Resources.Localization;
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
        string accountUrl,
        string siteUrl
    )
    {
        var lines = participants
            .Select(participant => new EmailBlock(
                AppStrings.EmailsActivitySignupParticipantHtml(
                    WebUtility.HtmlEncode(participant.FullName),
                    WebUtility.HtmlEncode(participant.RoleName)
                ),
                AppStrings.EmailsActivitySignupParticipantText(
                    participant.FullName,
                    participant.RoleName
                )
            ))
            .ToList();

        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsActivitySignupHeading,
                AppStrings.EmailsActivitySignupIntro,
                toName,
                siteUrl,
                EmailBranding.Info,
                AppStrings.EmailsFooterAutomaticNote
            ),
            [
                EmailBlocks.Prose(AppStrings.EmailsActivitySignupIntro),
                details.ToBlock(timeZone),
                EmailBlocks.Bullets(AppStrings.EmailsActivitySignupParticipantsLabel, lines),
                EmailBlocks.Callout(AppStrings.EmailsActivitySignupPending, EmailBranding.Info),
                EmailBlocks.Prose(AppStrings.EmailsActivitySignupAccountPrompt),
                EmailBlocks.Action(AppStrings.EmailsActivitySignupButtonLabel, accountUrl),
                EmailBlocks.Prose(
                    AppStrings.EmailsSharedSignoffHtml,
                    AppStrings.EmailsSharedSignoffText
                ),
            ]
        );

        return new EmailMessage(
            EmailKind.ActivityNotification,
            toAddress,
            toName,
            AppStrings.EmailsActivitySignupSubject(details.ActivityTitle),
            content.Html,
            content.Text,
            InlineImages: content.InlineImages
        );
    }
}
