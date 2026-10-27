namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Defines the inline CSS of the email layout and blocks.
/// </summary>
public static class EmailStyles
{
    private const string Font = $"font-family:{EmailBranding.FontStack};";

    /// <summary>
    /// Style of the body element, without margins and on the canvas color.
    /// </summary>
    public const string Body = $"margin:0;padding:0;background-color:{EmailBranding.Canvas};";

    /// <summary>
    /// Style of the full-width table that paints the canvas around the card.
    /// </summary>
    public const string Canvas = $"background-color:{EmailBranding.Canvas};";

    /// <summary>
    /// Style of the cell that centers the card and spaces it from the edges.
    /// </summary>
    public const string OuterCell = "padding:32px 12px;";

    /// <summary>
    /// Style of the bar at the top of the card; the layout appends the color of the accent.
    /// </summary>
    public const string AccentBar =
        "height:4px;line-height:4px;font-size:0;border-radius:13px 13px 0 0;";

    /// <summary>
    /// Style of the 600-pixel card that holds the header, the content and the footer.
    /// </summary>
    public const string Card =
        "width:600px;max-width:600px;border-radius:14px;"
        + $"background-color:{EmailBranding.Surface};border:1px solid {EmailBranding.Border};";

    /// <summary>
    /// Style that hides the preheader in the body while mail clients still show it as preview text.
    /// </summary>
    public const string Preheader =
        "display:none;max-height:0;max-width:0;opacity:0;overflow:hidden;"
        + $"mso-hide:all;font-size:1px;line-height:1px;color:{EmailBranding.Canvas};";

    /// <summary>
    /// Style of the header cell with the logo and the brand name.
    /// </summary>
    public const string HeadCell =
        $"padding:24px 32px 20px 32px;border-bottom:1px solid {EmailBranding.BorderSoft};";

    /// <summary>
    /// Style of the cell with the heading, the greeting and the blocks.
    /// </summary>
    public const string ContentCell = "padding:28px 32px 30px 32px;";

    /// <summary>
    /// Style of the footer cell.
    /// </summary>
    public const string FooterCell =
        "padding:22px 32px 26px 32px;border-radius:0 0 13px 13px;"
        + $"background-color:{EmailBranding.Canvas};border-top:1px solid {EmailBranding.BorderSoft};";

    /// <summary>
    /// Style of the 40-pixel logo in the header.
    /// </summary>
    public const string Logo = "display:block;width:40px;height:40px;";

    /// <summary>
    /// Style of the brand name next to the logo.
    /// </summary>
    public const string Brand =
        Font
        + "font-size:19px;font-weight:700;letter-spacing:-0.2px;"
        + $"color:{EmailBranding.TextBright};";

    /// <summary>
    /// Style of the heading of the message.
    /// </summary>
    public const string Heading =
        Font
        + "margin:0 0 18px 0;font-size:24px;line-height:1.25;font-weight:700;"
        + $"letter-spacing:-0.4px;color:{EmailBranding.TextBright};";

    /// <summary>
    /// Style of the brand name in the footer.
    /// </summary>
    public const string FooterBrand =
        Font + $"margin:0 0 6px 0;font-size:14px;font-weight:700;color:{EmailBranding.TextBright};";

    /// <summary>
    /// Style of the tagline in the footer.
    /// </summary>
    public const string FooterTagline =
        Font + $"margin:0 0 14px 0;font-size:13px;line-height:1.6;color:{EmailBranding.TextMuted};";

    /// <summary>
    /// Style of the footer paragraph that holds the site link.
    /// </summary>
    public const string FooterLinkWrap = Font + "margin:0;font-size:13px;line-height:1.6;";

    /// <summary>
    /// Style of the notices under the site link of a news email.
    /// </summary>
    public const string FooterNotice =
        Font + $"margin:14px 0 0 0;font-size:12px;line-height:1.6;color:{EmailBranding.TextMuted};";

    /// <summary>
    /// Style of the footer links: the site link and, in a news email, the unsubscribe and contact links.
    /// </summary>
    public const string FooterLink =
        $"color:{EmailBranding.PrimaryInk};font-weight:700;text-decoration:underline;";

    /// <summary>
    /// Style of a body paragraph, the greeting included.
    /// </summary>
    public const string Paragraph =
        Font + $"margin:0 0 16px 0;font-size:15px;line-height:1.65;color:{EmailBranding.Text};";

    /// <summary>
    /// Style of a note and of the line that writes out the link under a button.
    /// </summary>
    public const string Muted =
        Font + $"margin:0 0 16px 0;font-size:13px;line-height:1.6;color:{EmailBranding.TextMuted};";

    /// <summary>
    /// Style of the cell that paints a button.
    /// </summary>
    public const string ButtonCell =
        $"border-radius:10px;background-color:{EmailBranding.Primary};";

    /// <summary>
    /// Style of the link that forms the label of a button.
    /// </summary>
    public const string ButtonLink =
        Font
        + "display:inline-block;padding:13px 30px;font-size:15px;font-weight:700;"
        + $"line-height:1.2;text-decoration:none;border-radius:10px;color:{EmailBranding.OnPrimary};";

    /// <summary>
    /// Style of the box around a one-time code.
    /// </summary>
    public const string CodeCell =
        "padding:16px 28px;border-radius:10px;"
        + $"background-color:{EmailBranding.Panel};border:1px solid {EmailBranding.Border};";

    /// <summary>
    /// Style of the large, spaced monospace characters of a one-time code.
    /// </summary>
    public const string CodeText =
        "font-family:Consolas,'Courier New',monospace;font-size:32px;font-weight:700;"
        + $"letter-spacing:8px;line-height:1.2;color:{EmailBranding.TextBright};";

    /// <summary>
    /// Style of the link written out under a button, which breaks anywhere to fit narrow screens.
    /// </summary>
    public const string FallbackLink = $"color:{EmailBranding.PrimaryInk};word-break:break-all;";

    /// <summary>
    /// Style of the cell of a panel, such as the activity details.
    /// </summary>
    public const string PanelCell =
        "padding:4px 18px;border-radius:10px;"
        + $"background-color:{EmailBranding.Panel};border:1px solid {EmailBranding.Border};";

    /// <summary>
    /// Style of the cell of a callout; the block appends the left edge in the color of its accent.
    /// </summary>
    public const string CalloutCell =
        "padding:14px 18px;border-radius:10px;"
        + $"background-color:{EmailBranding.Panel};border:1px solid {EmailBranding.Border};";

    /// <summary>
    /// Style of the text of a callout.
    /// </summary>
    public const string CalloutText =
        Font + $"margin:0;font-size:14px;line-height:1.6;color:{EmailBranding.Text};";

    /// <summary>
    /// Style of the label cell of a detail row.
    /// </summary>
    public const string DetailsLabel =
        Font
        + "padding:11px 16px 11px 0;font-size:13px;line-height:1.5;vertical-align:top;"
        + $"white-space:nowrap;color:{EmailBranding.TextMuted};";

    /// <summary>
    /// Style of the value cell of a detail row.
    /// </summary>
    public const string DetailsValue =
        Font
        + "padding:11px 0;font-size:14px;line-height:1.5;font-weight:600;"
        + $"vertical-align:top;color:{EmailBranding.Text};";

    /// <summary>
    /// Style appended to the cells of every detail row but the first, to separate it from the one above.
    /// </summary>
    public const string DetailsRowBorder = $"border-top:1px solid {EmailBranding.Border};";
}
