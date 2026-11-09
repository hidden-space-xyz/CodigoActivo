using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.TermsDecisions;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the notice that tells the association consents given were erased with an account or an
/// event and what it has to stop doing, since they can no longer be consulted.
/// </summary>
public static class ErasedConsentEmail
{
    /// <summary>
    /// Creates the notice for the association's legal contact: for every consent erased, whose it
    /// was, for which document and event, who gave it and when, then a callout with what to stop
    /// using and a note that the decision stays blocked and encrypted. One consent reads in the
    /// singular, several in the plural.
    /// </summary>
    /// <param name="toAddress">Legal contact email of the association.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="consents">
    /// What was consented, for whom and by whom, at least one; its texts are raw and encoded for
    /// the HTML part.
    /// </param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <returns>
    /// The message, of kind <see cref="EmailKind.ConsentWithdrawalNotice"/>, the kind of the
    /// consent notices to the legal contact.
    /// </returns>
    /// <exception cref="ArgumentException">No consent was given.</exception>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        IReadOnlyList<ErasedConsent> consents,
        TimeZoneInfo timeZone,
        string siteUrl
    )
    {
        return ListNoticeEmail.Create(
            EmailKind.ConsentWithdrawalNotice,
            new ListNoticeEmail.Texts(
                AppStrings.EmailsConsentErasedSubject,
                AppStrings.EmailsConsentErasedSubjectMany,
                AppStrings.EmailsConsentErasedHeading,
                AppStrings.EmailsConsentErasedHeadingMany,
                AppStrings.EmailsConsentErasedAction,
                AppStrings.EmailsConsentErasedActionMany,
                AppStrings.EmailsConsentErasedBlockedNote,
                AppStrings.EmailsConsentErasedBlockedNoteMany
            ),
            consents,
            consent =>
                AppStrings.EmailsConsentErasedIntroText(
                    consent.PersonName,
                    consent.DocumentName,
                    consent.EventTitle
                ),
            consent =>
                AppStrings.EmailsConsentErasedIntroHtml(
                    WebUtility.HtmlEncode(consent.PersonName),
                    WebUtility.HtmlEncode(consent.DocumentName),
                    WebUtility.HtmlEncode(consent.EventTitle)
                ),
            (consent, local) =>
                AppStrings.EmailsConsentErasedGiven(consent.DecidedByName, local(consent.GivenAt)),
            toAddress,
            toName,
            timeZone,
            siteUrl
        );
    }
}
