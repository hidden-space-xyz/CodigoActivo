using System.Globalization;
using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Events;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the notice that tells the association a consent was withdrawn and what it has to stop
/// doing.
/// </summary>
public static class ConsentWithdrawalEmail
{
    private const string TimestampFormat = "dd/MM/yyyy, HH:mm";

    /// <summary>
    /// Creates the notice for the association's legal contact.
    /// </summary>
    /// <param name="toAddress">Legal contact email of the association.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="withdrawal">What was withdrawn, for whom and by whom.</param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <returns>The resulting email message value.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        ConsentWithdrawal withdrawal,
        TimeZoneInfo timeZone,
        string siteUrl
    )
    {
        ArgumentNullException.ThrowIfNull(withdrawal);
        var introText = AppStrings.EmailsConsentWithdrawnIntroText(
            withdrawal.PersonName,
            withdrawal.DocumentName,
            withdrawal.EventTitle
        );
        var withdrawnAt = TimeZoneInfo
            .ConvertTime(withdrawal.WithdrawnAt, timeZone)
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsConsentWithdrawnHeading,
                introText,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsConsentWithdrawnIntroHtml(
                        WebUtility.HtmlEncode(withdrawal.PersonName),
                        WebUtility.HtmlEncode(withdrawal.DocumentName),
                        WebUtility.HtmlEncode(withdrawal.EventTitle)
                    ),
                    introText
                ),
                EmailBlocks.Prose(
                    AppStrings.EmailsConsentWithdrawnRecorded(
                        withdrawal.RecordedByName,
                        withdrawnAt
                    )
                ),
                EmailBlocks.Callout(AppStrings.EmailsConsentWithdrawnAction, EmailBranding.Danger),
                EmailBlocks.Note(AppStrings.EmailsConsentWithdrawnHistoryNote),
            ]
        );

        return new EmailMessage(
            EmailKind.ConsentWithdrawalNotice,
            toAddress,
            toName,
            AppStrings.EmailsConsentWithdrawnSubject,
            content.Html,
            content.Text,
            InlineImages: content.InlineImages
        );
    }
}
