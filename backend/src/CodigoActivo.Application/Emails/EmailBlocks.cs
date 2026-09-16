using System.Net;
using CodigoActivo.Application.Resources.Localization;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Builds reusable HTML blocks for transactional emails.
/// </summary>
public static class EmailBlocks
{
    private const string TableOpen =
        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" "
        + "border=\"0\" style=\"margin:0 0 18px 0;\">";

    /// <summary>
    /// Builds an email paragraph content block.
    /// </summary>
    /// <param name="html">The html value.</param>
    /// <returns>The generated text.</returns>
    public static string Paragraph(string html)
    {
        return $"<p class=\"ca-text\" style=\"{EmailStyles.Paragraph}\">{html}</p>";
    }

    /// <summary>
    /// Builds an email prose content block.
    /// </summary>
    /// <param name="html">The html value.</param>
    /// <param name="text">The text value.</param>
    /// <returns>The resulting email block value.</returns>
    public static EmailBlock Prose(string html, string text)
    {
        return new EmailBlock(Paragraph(html), text);
    }

    /// <summary>
    /// Builds an email prose content block.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The resulting email block value.</returns>
    public static EmailBlock Prose(string text)
    {
        return Prose(WebUtility.HtmlEncode(text), text);
    }

    /// <summary>
    /// Builds an email note content block.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <returns>The resulting email block value.</returns>
    public static EmailBlock Note(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        return new EmailBlock(
            $"<p class=\"ca-muted\" style=\"{EmailStyles.Muted}\">{encoded}</p>",
            text
        );
    }

    /// <summary>
    /// Builds an email action content block.
    /// </summary>
    /// <param name="label">The label value.</param>
    /// <param name="url">The url value.</param>
    /// <returns>The resulting email block value.</returns>
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

    /// <summary>
    /// Builds an email callout content block.
    /// </summary>
    /// <param name="text">The text value.</param>
    /// <param name="accent">The accent value.</param>
    /// <returns>The resulting email block value.</returns>
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

    /// <summary>
    /// Builds an email panel content block.
    /// </summary>
    /// <param name="rows">The rows value.</param>
    /// <param name="text">The text value.</param>
    /// <returns>The resulting email block value.</returns>
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
}
