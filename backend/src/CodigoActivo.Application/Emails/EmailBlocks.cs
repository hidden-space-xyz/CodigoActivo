using System.Net;
using CodigoActivo.Application.Resources.Localization;

namespace CodigoActivo.Application.Emails;

public static class EmailBlocks
{
    private const string TableOpen =
        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" "
        + "border=\"0\" style=\"margin:0 0 18px 0;\">";

    public static string Paragraph(string html)
    {
        return $"<p class=\"ca-text\" style=\"{EmailStyles.Paragraph}\">{html}</p>";
    }

    public static EmailBlock Prose(string html, string text)
    {
        return new EmailBlock(Paragraph(html), text);
    }

    public static EmailBlock Prose(string text)
    {
        return Prose(WebUtility.HtmlEncode(text), text);
    }

    public static EmailBlock Note(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        return new EmailBlock(
            $"<p class=\"ca-muted\" style=\"{EmailStyles.Muted}\">{encoded}</p>",
            text
        );
    }

    public static EmailBlock Action(string label, string url)
    {
        var encodedUrl = WebUtility.HtmlEncode(url);
        var encodedLabel = WebUtility.HtmlEncode(label);
        var fallback = WebUtility.HtmlEncode(AppStrings.EmailsSharedFallbackLinkNote);

        var html = $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:4px auto 18px auto;">
            <tr>
            <td align="center" bgcolor="{EmailBranding.Primary}" style="{EmailStyles.ButtonCell}">
            <a href="{encodedUrl}" style="{EmailStyles.ButtonLink}">{encodedLabel}</a>
            </td>
            </tr>
            </table>
            <p class="ca-muted" style="{EmailStyles.Muted}">{fallback}<br>
            <a class="ca-link" href="{encodedUrl}" style="{EmailStyles.FallbackLink}">{encodedUrl}</a>
            </p>
            """;

        return new EmailBlock(html, url);
    }

    public static EmailBlock Code(string prompt, string code)
    {
        var encoded = WebUtility.HtmlEncode(code);

        var html = $"""
            {Paragraph(WebUtility.HtmlEncode(prompt))}
            {TableOpen}
            <tr>
            <td class="ca-otp" align="center" style="{EmailStyles.OtpCell}">
            <span class="ca-code" style="{EmailStyles.OtpCode}">{encoded}</span>
            </td>
            </tr>
            </table>
            """;

        return new EmailBlock(html, $"{prompt}\n\n{code}");
    }

    public static EmailBlock Callout(string text, EmailAccent accent)
    {
        var accentEdge = $"border-left:4px solid {accent.Line};";

        var html = $"""
            {TableOpen}
            <tr>
            <td class="ca-callout" style="{EmailStyles.CalloutCell}{accentEdge}">
            <p class="ca-text" style="{EmailStyles.CalloutText}">{WebUtility.HtmlEncode(text)}</p>
            </td>
            </tr>
            </table>
            """;

        return new EmailBlock(html, text);
    }

    public static EmailBlock Panel(string rows, string text)
    {
        var html = $"""
            {TableOpen}
            <tr>
            <td class="ca-panel" style="{EmailStyles.PanelCell}">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
            {rows}
            </table>
            </td>
            </tr>
            </table>
            """;

        return new EmailBlock(html, text);
    }

    public static EmailBlock Bullets(string label, IReadOnlyList<EmailBlock> items)
    {
        var rows = string.Concat(
            items.Select(item => $"""
                <tr>
                <td width="16" valign="top" style="{EmailStyles.BulletMark}">&bull;</td>
                <td class="ca-text" valign="top" style="{EmailStyles.BulletText}">{item.Html}</td>
                </tr>
                """)
        );

        var encodedLabel = WebUtility.HtmlEncode(label);

        var html = $"""
            <p class="ca-text" style="{EmailStyles.TightParagraph}">{encodedLabel}</p>
            {TableOpen}
            {rows}
            </table>
            """;

        var text = string.Join("\n", new[] { label }.Concat(items.Select(item => item.Text)));

        return new EmailBlock(html, text);
    }
}
