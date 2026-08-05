using System.Net;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public sealed record ManualEmailContent(
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailInlineImage> InlineImages
);

public static class ManualEmail
{
    private const int PreheaderLength = 140;

    private static readonly char[] Whitespace = [' ', '\t', '\n', '\r', '\f', '\v'];

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

        return new ManualEmailContent(
            subject,
            content.Html,
            content.Text,
            content.InlineImages
        );
    }

    public static EmailMessage Create(
        ManualEmailContent content,
        string toAddress,
        string toName,
        IReadOnlyList<EmailAttachment> attachments
    )
    {
        return new EmailMessage(
            EmailKind.Manual,
            toAddress,
            toName,
            content.Subject,
            content.HtmlBody,
            content.TextBody,
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
        return WebUtility
            .HtmlEncode(paragraph)
            .Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static string Preheader(string body)
    {
        var flat = string.Join(
            ' ',
            body.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries)
        );

        if (flat.Length <= PreheaderLength)
        {
            return flat;
        }

        var cut = flat[..PreheaderLength];
        var lastSpace = cut.LastIndexOf(' ');
        return lastSpace > 0 ? cut[..lastSpace] : cut;
    }
}
