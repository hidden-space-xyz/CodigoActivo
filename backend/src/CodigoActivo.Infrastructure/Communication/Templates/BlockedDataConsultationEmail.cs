using System.Globalization;
using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Domain.BlockedData;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the notice that tells the association consultations of the blocked data were opened.
/// </summary>
public static class BlockedDataConsultationEmail
{
    private const string TimestampFormat = "dd/MM/yyyy, HH:mm";

    /// <summary>
    /// Creates the notice for the association's legal contact: for every consultation, who opened
    /// it, for which authority and reference, and when, then a note on who may receive the blocked
    /// data. One consultation reads in the singular, several in the plural.
    /// </summary>
    /// <param name="toAddress">Legal contact email of the association.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="consultations">
    /// The consultations opened, at least one; their texts are raw and encoded for the HTML part.
    /// </param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <returns>The message, of kind <see cref="EmailKind.BlockedDataConsultationNotice"/>.</returns>
    /// <exception cref="ArgumentException">No consultation was given.</exception>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        IReadOnlyList<OpenedConsultation> consultations,
        TimeZoneInfo timeZone,
        string siteUrl
    )
    {
        ArgumentNullException.ThrowIfNull(consultations);
        if (consultations.Count is 0)
        {
            throw new ArgumentException(
                "A notice needs at least one consultation.",
                nameof(consultations)
            );
        }

        var single = consultations.Count is 1;
        var subject = single
            ? AppStrings.EmailsBlockedDataConsultationSubject
            : AppStrings.EmailsBlockedDataConsultationSubjectMany(consultations.Count);
        var content = EmailLayout.Render(
            new EmailDocument(
                single
                    ? AppStrings.EmailsBlockedDataConsultationHeading
                    : AppStrings.EmailsBlockedDataConsultationHeadingMany,
                single ? IntroText(consultations[0]) : subject,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                .. consultations.SelectMany(consultation => Describe(consultation, timeZone)),
                EmailBlocks.Note(
                    single
                        ? AppStrings.EmailsBlockedDataConsultationNote
                        : AppStrings.EmailsBlockedDataConsultationNoteMany
                ),
            ]
        );

        return content.ToMessage(
            EmailKind.BlockedDataConsultationNotice,
            toAddress,
            toName,
            subject
        );
    }

    private static string IntroText(OpenedConsultation consultation)
    {
        return AppStrings.EmailsBlockedDataConsultationIntroText(
            consultation.OfficerName,
            AuthorityName(consultation.Authority),
            consultation.Reference
        );
    }

    private static IEnumerable<EmailBlock> Describe(
        OpenedConsultation consultation,
        TimeZoneInfo timeZone
    )
    {
        var openedAt = TimeZoneInfo
            .ConvertTime(consultation.OpenedAt, timeZone)
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);

        yield return EmailBlocks.Prose(
            AppStrings.EmailsBlockedDataConsultationIntroHtml(
                WebUtility.HtmlEncode(consultation.OfficerName),
                WebUtility.HtmlEncode(AuthorityName(consultation.Authority)),
                WebUtility.HtmlEncode(consultation.Reference)
            ),
            IntroText(consultation)
        );
        yield return EmailBlocks.Prose(AppStrings.EmailsBlockedDataConsultationOpened(openedAt));
    }

    private static string AuthorityName(BlockedDataAuthority authority)
    {
        return authority switch
        {
            BlockedDataAuthority.Court => AppStrings.EmailsBlockedDataConsultationAuthorityCourt,
            BlockedDataAuthority.Prosecutor =>
                AppStrings.EmailsBlockedDataConsultationAuthorityProsecutor,
            BlockedDataAuthority.DataProtectionAuthority =>
                AppStrings.EmailsBlockedDataConsultationAuthorityDataProtectionAuthority,
            BlockedDataAuthority.PublicAdministration =>
                AppStrings.EmailsBlockedDataConsultationAuthorityPublicAdministration,
            _ => AppStrings.EmailsBlockedDataConsultationAuthorityClaim,
        };
    }
}
