using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Rendered email written by an administrator, shared by every recipient of its batch.
/// </summary>
/// <param name="Subject">Subject as the administrator wrote it.</param>
/// <param name="HtmlBody">
/// HTML part; in a news email it keeps the place that <see cref="EmailLayout.LinkUnsubscribe"/> fills
/// with the unsubscribe link of each recipient.
/// </param>
/// <param name="TextBody">Plain-text part, with the same place in a news email.</param>
/// <param name="InlineImages">Images the HTML part references by content identifier: the logo.</param>
public sealed record ManualEmailContent(
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailInlineImage> InlineImages
);

/// <summary>
/// Builds the email an administrator writes, in the site layout.
/// </summary>
public static class ManualEmail
{
    private const int PreheaderLength = 140;

    private static readonly char[] Whitespace = [' ', '\t', '\n', '\r', '\f', '\v'];

    /// <summary>
    /// Renders the subject and body into the site layout, without a greeting. The preheader is the body
    /// on one line, cut to at most 140 characters at the last space that fits.
    /// </summary>
    /// <param name="subject">Subject, also the heading of the message; raw text.</param>
    /// <param name="body">
    /// Raw plain-text body; empty lines separate paragraphs and the line breaks inside one are kept.
    /// </param>
    /// <param name="siteUrl">Public base URL of the site, linked from the footer.</param>
    /// <param name="newsContact">
    /// Contact address of a news email, which carries the news footer;
    /// <see langword="null"/> for a notice.
    /// </param>
    /// <returns>The rendered content, shared by every recipient.</returns>
    public static ManualEmailContent Render(
        string subject,
        string body,
        string siteUrl,
        string? newsContact = null
    )
    {
        var paragraphs = Paragraphs(body);

        var content = EmailLayout.Render(
            new EmailDocument(
                subject,
                Preheader(body),
                null,
                siteUrl,
                EmailBranding.Brand,
                newsContact
            ),
            [.. paragraphs.Select(paragraph => EmailBlocks.Prose(ToHtml(paragraph), paragraph))]
        );

        return new ManualEmailContent(subject, content.Html, content.Text, content.InlineImages);
    }

    /// <summary>
    /// Addresses the rendered content to the recipients as one batch, so every recipient receives their
    /// own message while the stored copy of the content and its attachments is shared.
    /// </summary>
    /// <param name="content">Content rendered by <see cref="Render"/>.</param>
    /// <param name="recipients">Recipients, each with their own unsubscribe links in a news email.</param>
    /// <param name="attachments">Files attached to every message.</param>
    /// <returns>The batch, of kind <see cref="EmailKind.Manual"/>.</returns>
    public static EmailBatch Create(
        ManualEmailContent content,
        IReadOnlyList<EmailRecipient> recipients,
        IReadOnlyList<EmailAttachment> attachments
    )
    {
        ArgumentNullException.ThrowIfNull(content);

        return new EmailBatch(
            EmailKind.Manual,
            content.Subject,
            content.HtmlBody,
            content.TextBody,
            recipients,
            attachments,
            content.InlineImages
        );
    }

    private static string[] Paragraphs(string body)
    {
        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return normalized.Split(
            "\n\n",
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );
    }

    private static string ToHtml(string paragraph)
    {
        return WebUtility.HtmlEncode(paragraph).Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static string Preheader(string body)
    {
        var flat = string.Join(' ', body.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries));

        if (flat.Length <= PreheaderLength)
        {
            return flat;
        }

        var cut = flat[..PreheaderLength];
        var lastSpace = cut.LastIndexOf(' ');
        return lastSpace > 0 ? cut[..lastSpace] : cut;
    }
}
