using System.Globalization;
using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.TermsDecisions;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the notice that tells the association consents were withdrawn and what it has to stop
/// doing.
/// </summary>
public static class ConsentWithdrawalEmail
{
    private const string TimestampFormat = "dd/MM/yyyy, HH:mm";

    /// <summary>
    /// Creates the notice for the association's legal contact: for every withdrawal, whose consent
    /// was withdrawn, for which document and event, who recorded it and when, then a callout with
    /// what to stop using. One withdrawal reads in the singular, several in the plural.
    /// </summary>
    /// <param name="toAddress">Legal contact email of the association.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="withdrawals">
    /// What was withdrawn, for whom and by whom, at least one; its texts are raw and encoded for the
    /// HTML part.
    /// </param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <returns>The message, of kind <see cref="EmailKind.ConsentWithdrawalNotice"/>.</returns>
    /// <exception cref="ArgumentException">No withdrawal was given.</exception>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        IReadOnlyList<ConsentWithdrawal> withdrawals,
        TimeZoneInfo timeZone,
        string siteUrl
    )
    {
        ArgumentNullException.ThrowIfNull(withdrawals);
        if (withdrawals.Count is 0)
        {
            throw new ArgumentException(
                "A notice needs at least one withdrawal.",
                nameof(withdrawals)
            );
        }

        var single = withdrawals.Count is 1;
        var subject = single
            ? AppStrings.EmailsConsentWithdrawnSubject
            : AppStrings.EmailsConsentWithdrawnSubjectMany(withdrawals.Count);
        var content = EmailLayout.Render(
            new EmailDocument(
                single
                    ? AppStrings.EmailsConsentWithdrawnHeading
                    : AppStrings.EmailsConsentWithdrawnHeadingMany,
                single ? IntroText(withdrawals[0]) : subject,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                .. withdrawals.SelectMany(withdrawal => Describe(withdrawal, timeZone)),
                EmailBlocks.Callout(
                    single
                        ? AppStrings.EmailsConsentWithdrawnAction
                        : AppStrings.EmailsConsentWithdrawnActionMany,
                    EmailBranding.Danger
                ),
                EmailBlocks.Note(
                    single
                        ? AppStrings.EmailsConsentWithdrawnHistoryNote
                        : AppStrings.EmailsConsentWithdrawnHistoryNoteMany
                ),
            ]
        );

        return content.ToMessage(EmailKind.ConsentWithdrawalNotice, toAddress, toName, subject);
    }

    private static string IntroText(ConsentWithdrawal withdrawal)
    {
        return AppStrings.EmailsConsentWithdrawnIntroText(
            withdrawal.PersonName,
            withdrawal.DocumentName,
            withdrawal.EventTitle
        );
    }

    private static IEnumerable<EmailBlock> Describe(
        ConsentWithdrawal withdrawal,
        TimeZoneInfo timeZone
    )
    {
        var withdrawnAt = TimeZoneInfo
            .ConvertTime(withdrawal.WithdrawnAt, timeZone)
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);

        yield return EmailBlocks.Prose(
            AppStrings.EmailsConsentWithdrawnIntroHtml(
                WebUtility.HtmlEncode(withdrawal.PersonName),
                WebUtility.HtmlEncode(withdrawal.DocumentName),
                WebUtility.HtmlEncode(withdrawal.EventTitle)
            ),
            IntroText(withdrawal)
        );
        yield return EmailBlocks.Prose(
            AppStrings.EmailsConsentWithdrawnRecorded(withdrawal.RecordedByName, withdrawnAt)
        );
    }
}
