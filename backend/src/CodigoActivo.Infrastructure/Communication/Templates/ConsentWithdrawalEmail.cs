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
    /// <summary>
    /// Creates the notice for the association's legal contact: for every withdrawal, whose consent
    /// was withdrawn, for which document and event, who recorded it and when, then a callout with
    /// what to stop using and a note on where the earlier acceptance stays recorded. One withdrawal
    /// reads in the singular, several in the plural.
    /// </summary>
    /// <param name="toAddress">Legal contact email of the association.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="withdrawals">
    /// What was withdrawn, for whom and by whom, at least one; its texts are raw and encoded for
    /// the HTML part.
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
        return ListNoticeEmail.Create(
            EmailKind.ConsentWithdrawalNotice,
            new ListNoticeEmail.Texts(
                AppStrings.EmailsConsentWithdrawnSubject,
                AppStrings.EmailsConsentWithdrawnSubjectMany,
                AppStrings.EmailsConsentWithdrawnHeading,
                AppStrings.EmailsConsentWithdrawnHeadingMany,
                AppStrings.EmailsConsentWithdrawnAction,
                AppStrings.EmailsConsentWithdrawnActionMany,
                AppStrings.EmailsConsentWithdrawnHistoryNote,
                AppStrings.EmailsConsentWithdrawnHistoryNoteMany
            ),
            withdrawals,
            withdrawal =>
                AppStrings.EmailsConsentWithdrawnIntroText(
                    withdrawal.PersonName,
                    withdrawal.DocumentName,
                    withdrawal.EventTitle
                ),
            withdrawal =>
                AppStrings.EmailsConsentWithdrawnIntroHtml(
                    WebUtility.HtmlEncode(withdrawal.PersonName),
                    WebUtility.HtmlEncode(withdrawal.DocumentName),
                    WebUtility.HtmlEncode(withdrawal.EventTitle)
                ),
            (withdrawal, local) =>
                AppStrings.EmailsConsentWithdrawnRecorded(
                    withdrawal.RecordedByName,
                    local(withdrawal.WithdrawnAt)
                ),
            toAddress,
            toName,
            timeZone,
            siteUrl
        );
    }
}
