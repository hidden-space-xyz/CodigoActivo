using System.Net;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Builds the reusable blocks of an email body, each in its HTML and plain-text form.
/// </summary>
public static class EmailBlocks
{
    private const string TableOpen =
        "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" "
        + "border=\"0\" style=\"margin:0 0 18px 0;\">";

    /// <summary>
    /// Wraps HTML in a paragraph with the body text style.
    /// </summary>
    /// <param name="html">
    /// Content of the paragraph as HTML, inserted as is; the caller encodes any raw text.
    /// </param>
    /// <returns>The HTML paragraph element.</returns>
    public static string Paragraph(string html)
    {
        return $"<p class=\"ca-text\" style=\"{EmailStyles.Paragraph}\">{html}</p>";
    }

    /// <summary>
    /// Builds a paragraph block from HTML that may carry markup and its plain-text form.
    /// </summary>
    /// <param name="html">
    /// Content of the paragraph as HTML, inserted as is; the caller encodes any raw text.
    /// </param>
    /// <param name="text">
    /// Plain-text form of the paragraph; a blank text leaves the block out of the email.
    /// </param>
    /// <returns>The paragraph block.</returns>
    public static EmailBlock Prose(string html, string text)
    {
        return new EmailBlock(Paragraph(html), text);
    }

    /// <summary>
    /// Builds the sign-off of the association's team that closes the prose of the automatic emails to
    /// account holders; the notices to the association carry none.
    /// </summary>
    /// <returns>The sign-off paragraph block.</returns>
    public static EmailBlock Signoff()
    {
        return Prose(AppStrings.EmailsSharedSignoffHtml, AppStrings.EmailsSharedSignoffText);
    }

    /// <summary>
    /// Builds a paragraph block from raw text, encoded for the HTML part.
    /// </summary>
    /// <param name="text">Raw text of the paragraph; a blank text leaves the block out of the email.</param>
    /// <returns>The paragraph block.</returns>
    public static EmailBlock Prose(string text)
    {
        return Prose(WebUtility.HtmlEncode(text), text);
    }

    /// <summary>
    /// Builds a small muted paragraph for a side remark, such as what to do with an unexpected email.
    /// </summary>
    /// <param name="text">Raw text of the note, encoded for the HTML part.</param>
    /// <returns>The note block.</returns>
    public static EmailBlock Note(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        return new EmailBlock(
            $"<p class=\"ca-muted\" style=\"{EmailStyles.Muted}\">{encoded}</p>",
            text
        );
    }

    /// <summary>
    /// Builds a button that opens a link, followed by the link written out for clients where the button
    /// does not work. The plain-text form is the link alone.
    /// </summary>
    /// <param name="label">Raw text of the button, encoded for the HTML part.</param>
    /// <param name="url">Absolute link the button opens, encoded for the HTML part.</param>
    /// <returns>The button block.</returns>
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
    /// Builds an email block that displays a short one-time code prominently.
    /// </summary>
    /// <param name="code">Code exactly as the user must type it, encoded for the HTML part.</param>
    /// <returns>The code block, whose plain-text form is the code alone.</returns>
    public static EmailBlock Code(string code)
    {
        var encoded = WebUtility.HtmlEncode(code);

        var html = $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:4px auto 18px auto;">
            <tr>
            <td align="center" class="ca-code" style="{EmailStyles.CodeCell}">
            <span style="{EmailStyles.CodeText}">{encoded}</span>
            </td>
            </tr>
            </table>
            """;

        return new EmailBlock(html, code);
    }

    /// <summary>
    /// Builds a highlighted panel with a colored left edge for the sentence the reader must not miss.
    /// </summary>
    /// <param name="text">Raw text of the callout, encoded for the HTML part.</param>
    /// <param name="accent">Accent whose line color draws the left edge.</param>
    /// <returns>The callout block.</returns>
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
    /// Builds a bordered panel around a table of rows.
    /// </summary>
    /// <param name="rows">HTML table rows inserted as is; the caller encodes their content.</param>
    /// <param name="text">Plain-text form of the panel.</param>
    /// <returns>The panel block.</returns>
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
