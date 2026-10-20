using System.Globalization;
using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the notice that tells a guardian on which day a dependent comes of age and is erased, and
/// where to download its data before.
/// </summary>
public static class ComingOfAgeNoticeEmail
{
    private const string DateFormat = "dd/MM/yyyy";

    /// <summary>
    /// Creates the notice for the guardian.
    /// </summary>
    /// <param name="toAddress">Address of the guardian.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="dependentName">Full name of the dependent.</param>
    /// <param name="comingOfAgeOn">Day the dependent comes of age and is erased.</param>
    /// <param name="accountUrl">«Mi cuenta» page, where the guardian downloads the data.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <returns>The resulting email message value.</returns>
    public static EmailMessage Create(
        string toAddress,
        string toName,
        string dependentName,
        DateOnly comingOfAgeOn,
        string accountUrl,
        string siteUrl
    )
    {
        var day = comingOfAgeOn.ToString(DateFormat, CultureInfo.InvariantCulture);
        var introText = AppStrings.EmailsComingOfAgeIntroText(dependentName, day);
        var content = EmailLayout.Render(
            new EmailDocument(
                AppStrings.EmailsComingOfAgeHeading,
                introText,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                EmailBlocks.Prose(
                    AppStrings.EmailsComingOfAgeIntroHtml(
                        WebUtility.HtmlEncode(dependentName),
                        day
                    ),
                    introText
                ),
                EmailBlocks.Prose(AppStrings.EmailsComingOfAgeDownload),
                EmailBlocks.Action(AppStrings.EmailsComingOfAgeButtonLabel, accountUrl),
                EmailBlocks.Prose(AppStrings.EmailsComingOfAgeOwnAccount),
                EmailBlocks.Signoff(),
                EmailBlocks.Note(AppStrings.EmailsComingOfAgeBlockedCopyNote),
            ]
        );

        return content.ToMessage(
            EmailKind.ComingOfAgeNotice,
            toAddress,
            toName,
            AppStrings.EmailsComingOfAgeSubject
        );
    }
}
