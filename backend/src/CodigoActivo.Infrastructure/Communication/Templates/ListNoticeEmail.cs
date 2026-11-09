using System.Globalization;
using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the notices to the association's legal contact that list what happened: for every item,
/// its introduction and the line that dates it, then an optional callout and a closing note. One
/// item reads in the singular, several in the plural.
/// </summary>
internal static class ListNoticeEmail
{
    private const string TimestampFormat = "dd/MM/yyyy, HH:mm";

    /// <summary>
    /// Creates the notice.
    /// </summary>
    /// <typeparam name="TItem">Kind of the items listed.</typeparam>
    /// <param name="kind">Kind of the message.</param>
    /// <param name="texts">Texts of the notice in the singular and the plural.</param>
    /// <param name="items">
    /// Items to list, at least one; their texts are raw and encoded for the HTML part.
    /// </param>
    /// <param name="introText">Plain-text introduction of an item.</param>
    /// <param name="introHtml">HTML introduction of an item, with its texts encoded.</param>
    /// <param name="dated">
    /// Line that dates an item, given the item and a function that formats an instant in local time.
    /// </param>
    /// <param name="toAddress">Legal contact email of the association.</param>
    /// <param name="toName">Name the message greets.</param>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentException">No item was given.</exception>
    internal static EmailMessage Create<TItem>(
        EmailKind kind,
        Texts texts,
        IReadOnlyList<TItem> items,
        Func<TItem, string> introText,
        Func<TItem, string> introHtml,
        Func<TItem, Func<DateTimeOffset, string>, string> dated,
        string toAddress,
        string toName,
        TimeZoneInfo timeZone,
        string siteUrl
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count is 0)
        {
            throw new ArgumentException("A notice needs at least one item.", nameof(items));
        }

        var single = items.Count is 1;
        var subject = single ? texts.Subject : texts.SubjectMany(items.Count);
        string Local(DateTimeOffset instant) =>
            TimeZoneInfo
                .ConvertTime(instant, timeZone)
                .ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var action = single ? texts.Action : texts.ActionMany;

        var content = EmailLayout.Render(
            new EmailDocument(
                single ? texts.Heading : texts.HeadingMany,
                single ? introText(items[0]) : subject,
                toName,
                siteUrl,
                EmailBranding.Brand
            ),
            [
                .. items.SelectMany<TItem, EmailBlock>(item =>
                    [
                        EmailBlocks.Prose(introHtml(item), introText(item)),
                        EmailBlocks.Prose(dated(item, Local)),
                    ]
                ),
                .. (
                    action is null
                        ? Array.Empty<EmailBlock>()
                        : [EmailBlocks.Callout(action, EmailBranding.Danger)]
                ),
                EmailBlocks.Note(single ? texts.Note : texts.NoteMany),
            ]
        );

        return content.ToMessage(kind, toAddress, toName, subject);
    }

    /// <summary>
    /// Texts of a notice in the singular and the plural.
    /// </summary>
    /// <param name="Subject">Subject of a notice about one item.</param>
    /// <param name="SubjectMany">Subject of a notice about several items, by their count.</param>
    /// <param name="Heading">Heading of a notice about one item.</param>
    /// <param name="HeadingMany">Heading of a notice about several items.</param>
    /// <param name="Action">Callout of a notice about one item, if any.</param>
    /// <param name="ActionMany">Callout of a notice about several items, if any.</param>
    /// <param name="Note">Closing note of a notice about one item.</param>
    /// <param name="NoteMany">Closing note of a notice about several items.</param>
    internal sealed record Texts(
        string Subject,
        Func<int, string> SubjectMany,
        string Heading,
        string HeadingMany,
        string? Action,
        string? ActionMany,
        string Note,
        string NoteMany
    );
}
