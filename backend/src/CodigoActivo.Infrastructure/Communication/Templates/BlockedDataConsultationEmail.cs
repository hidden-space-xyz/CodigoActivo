using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the notice that tells the association consultations of the blocked data were opened.
/// </summary>
public static class BlockedDataConsultationEmail
{
    /// <summary>
    /// Creates the notice for the association's legal contact: for every consultation, who opened
    /// it, who asks for the data, the reference and when, then a note on what to do if nobody
    /// expected it. One consultation reads in the singular, several in the plural.
    /// </summary>
    /// <param name="toAddress">Legal contact email of the association.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="consultations">
    /// The consultations opened, at least one; their texts are raw and encoded for the HTML part.
    /// </param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <returns>
    /// The message, of kind <see cref="EmailKind.BlockedDataConsultationNotice"/>.
    /// </returns>
    /// <exception cref="ArgumentException">No consultation was given.</exception>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        IReadOnlyList<OpenedConsultation> consultations,
        TimeZoneInfo timeZone,
        string siteUrl
    )
    {
        return ListNoticeEmail.Create(
            EmailKind.BlockedDataConsultationNotice,
            new ListNoticeEmail.Texts(
                AppStrings.EmailsBlockedDataConsultationSubject,
                AppStrings.EmailsBlockedDataConsultationSubjectMany,
                AppStrings.EmailsBlockedDataConsultationHeading,
                AppStrings.EmailsBlockedDataConsultationHeadingMany,
                null,
                null,
                AppStrings.EmailsBlockedDataConsultationNote,
                AppStrings.EmailsBlockedDataConsultationNoteMany
            ),
            consultations,
            consultation =>
                AppStrings.EmailsBlockedDataConsultationIntroText(
                    consultation.OfficerName,
                    consultation.Requester,
                    consultation.Reference
                ),
            consultation =>
                AppStrings.EmailsBlockedDataConsultationIntroHtml(
                    WebUtility.HtmlEncode(consultation.OfficerName),
                    WebUtility.HtmlEncode(consultation.Requester),
                    WebUtility.HtmlEncode(consultation.Reference)
                ),
            (consultation, local) =>
                AppStrings.EmailsBlockedDataConsultationOpened(local(consultation.OpenedAt)),
            toAddress,
            toName,
            timeZone,
            siteUrl
        );
    }
}
