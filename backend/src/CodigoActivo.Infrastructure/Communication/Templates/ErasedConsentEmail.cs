using System.Globalization;
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
    private const string TimestampFormat = "dd/MM/yyyy, HH:mm";

    /// <summary>
    /// Creates the notice for the association's legal contact: for every consent erased, whose it
    /// was, for which document and event, who gave it and when, then a callout with what to stop
    /// using. One consent reads in the singular, several in the plural.
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
        ArgumentNullException.ThrowIfNull(consents);
        if (consents.Count is 0)
        {
            throw new ArgumentException("A notice needs at least one consent.", nameof(consents));
        }

        var single = consents.Count is 1;
        var subject = single
            ? AppStrings.EmailsConsentErasedSubject
            : AppStrings.EmailsConsentErasedSubjectMany(consents.Count);
        var content = EmailLayout.Render(
            new EmailDocument(
                single
                    ? AppStrings.EmailsConsentErasedHeading
                    : AppStrings.EmailsConsentErasedHeadingMany,
                single ? IntroText(consents[0]) : subject,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                .. consents.SelectMany(consent => Describe(consent, timeZone)),
                EmailBlocks.Callout(
                    single
                        ? AppStrings.EmailsConsentErasedAction
                        : AppStrings.EmailsConsentErasedActionMany,
                    EmailBranding.Danger
                ),
                EmailBlocks.Note(
                    single
                        ? AppStrings.EmailsConsentErasedBlockedNote
                        : AppStrings.EmailsConsentErasedBlockedNoteMany
                ),
            ]
        );

        return content.ToMessage(EmailKind.ConsentWithdrawalNotice, toAddress, toName, subject);
    }

    private static string IntroText(ErasedConsent consent)
    {
        return AppStrings.EmailsConsentErasedIntroText(
            consent.PersonName,
            consent.DocumentName,
            consent.EventTitle
        );
    }

    private static IEnumerable<EmailBlock> Describe(ErasedConsent consent, TimeZoneInfo timeZone)
    {
        var givenAt = TimeZoneInfo
            .ConvertTime(consent.GivenAt, timeZone)
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);

        yield return EmailBlocks.Prose(
            AppStrings.EmailsConsentErasedIntroHtml(
                WebUtility.HtmlEncode(consent.PersonName),
                WebUtility.HtmlEncode(consent.DocumentName),
                WebUtility.HtmlEncode(consent.EventTitle)
            ),
            IntroText(consent)
        );
        yield return EmailBlocks.Prose(
            AppStrings.EmailsConsentErasedGiven(consent.DecidedByName, givenAt)
        );
    }
}
