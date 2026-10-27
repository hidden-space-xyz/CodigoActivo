using System.Globalization;
using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Domain.BlockedData;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the notice that tells the association a consultation of the blocked data was opened.
/// </summary>
public static class BlockedDataConsultationEmail
{
    private const string TimestampFormat = "dd/MM/yyyy, HH:mm";

    /// <summary>
    /// Creates the notice for the association's legal contact: who opened the consultation, for which
    /// authority and reference, and when, with a note on who may receive the blocked data.
    /// </summary>
    /// <param name="toAddress">Legal contact email of the association.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="consultation">
    /// The consultation opened; its texts are raw and encoded for the HTML part.
    /// </param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <returns>The message, of kind <see cref="EmailKind.BlockedDataConsultationNotice"/>.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        OpenedConsultation consultation,
        TimeZoneInfo timeZone,
        string siteUrl
    )
    {
        ArgumentNullException.ThrowIfNull(consultation);
        var authority = AuthorityName(consultation.Authority);
        var introText = AppStrings.EmailsBlockedDataConsultationIntroText(
            consultation.OfficerName,
            authority,
            consultation.Reference
        );
        var openedAt = TimeZoneInfo
            .ConvertTime(consultation.OpenedAt, timeZone)
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsBlockedDataConsultationHeading,
                introText,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsBlockedDataConsultationIntroHtml(
                        WebUtility.HtmlEncode(consultation.OfficerName),
                        WebUtility.HtmlEncode(authority),
                        WebUtility.HtmlEncode(consultation.Reference)
                    ),
                    introText
                ),
                EmailBlocks.Prose(AppStrings.EmailsBlockedDataConsultationOpened(openedAt)),
                EmailBlocks.Note(AppStrings.EmailsBlockedDataConsultationNote),
            ]
        );

        return content.ToMessage(
            EmailKind.BlockedDataConsultationNotice,
            toAddress,
            toName,
            AppStrings.EmailsBlockedDataConsultationSubject
        );
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
