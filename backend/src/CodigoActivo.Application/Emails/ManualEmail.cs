using System.Net;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Represents a manual email content value used by the application.
/// </summary>
/// <param name="Subject">The subject value.</param>
/// <param name="HtmlBody">The html body value.</param>
/// <param name="TextBody">The text body value.</param>
/// <param name="InlineImages">The inline images value.</param>
public sealed record ManualEmailContent(
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailInlineImage> InlineImages
);

/// <summary>
/// Builds the email content for manual.
/// </summary>
public static class ManualEmail
{
    private const int PreheaderLength = 140;

    private static readonly char[] Whitespace = [' ', '\t', '\n', '\r', '\f', '\v'];

    /// <summary>
    /// Builds the render output.
    /// </summary>
    /// <param name="subject">The subject value.</param>
    /// <param name="body">The body value.</param>
    /// <param name="siteUrl">The site url value.</param>
    /// <returns>The resulting manual email content value.</returns>
    public static ManualEmailContent Render(string subject, string body, string siteUrl)
    {
        var paragraphs = Paragraphs(body);

        var content = EmailLayout.Render(
            new EmailDocument(
                subject,
                Preheader(body),
                null,
                siteUrl,
                EmailBranding.Brand,
                AppStrings.EmailsManualSignature
            ),
            [.. paragraphs.Select(paragraph => EmailBlocks.Prose(ToHtml(paragraph), paragraph))]
        );

        return new ManualEmailContent(subject, content.Html, content.Text, content.InlineImages);
    }

    /// <summary>
    /// Creates the manual email batch from the validated request, so every recipient receives their
    /// own message while the stored copy of the content and its attachments is shared.
    /// </summary>
    /// <param name="content">Content stream to store or inspect.</param>
    /// <param name="recipients">The recipients value.</param>
    /// <param name="attachments">The attachments value.</param>
    /// <returns>The resulting email batch value.</returns>
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
