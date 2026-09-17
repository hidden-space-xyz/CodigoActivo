namespace CodigoActivo.Application.Emails;

/// <summary>
/// Defines the shared CSS used by transactional emails.
/// </summary>
public static class EmailStyles
{
    private const string Font = $"font-family:{EmailBranding.FontStack};";

    /// <summary>
    /// Identifies the body configuration or policy value.
    /// </summary>
    public const string Body = $"margin:0;padding:0;background-color:{EmailBranding.Canvas};";

    /// <summary>
    /// Identifies the canvas configuration or policy value.
    /// </summary>
    public const string Canvas = $"background-color:{EmailBranding.Canvas};";

    /// <summary>
    /// Identifies the outer cell configuration or policy value.
    /// </summary>
    public const string OuterCell = "padding:32px 12px;";

    /// <summary>
    /// Identifies the accent bar configuration or policy value.
    /// </summary>
    public const string AccentBar =
        "height:4px;line-height:4px;font-size:0;border-radius:13px 13px 0 0;";

    /// <summary>
    /// Identifies the card configuration or policy value.
    /// </summary>
    public const string Card =
        "width:600px;max-width:600px;border-radius:14px;"
        + $"background-color:{EmailBranding.Surface};border:1px solid {EmailBranding.Border};";

    /// <summary>
    /// Identifies the preheader configuration or policy value.
    /// </summary>
    public const string Preheader =
        "display:none;max-height:0;max-width:0;opacity:0;overflow:hidden;"
        + $"mso-hide:all;font-size:1px;line-height:1px;color:{EmailBranding.Canvas};";

    /// <summary>
    /// Identifies the head cell configuration or policy value.
    /// </summary>
    public const string HeadCell =
        $"padding:24px 32px 20px 32px;border-bottom:1px solid {EmailBranding.BorderSoft};";

    /// <summary>
    /// Identifies the content cell configuration or policy value.
    /// </summary>
    public const string ContentCell = "padding:28px 32px 30px 32px;";

    /// <summary>
    /// Identifies the footer cell configuration or policy value.
    /// </summary>
    public const string FooterCell =
        "padding:22px 32px 26px 32px;border-radius:0 0 13px 13px;"
        + $"background-color:{EmailBranding.Canvas};border-top:1px solid {EmailBranding.BorderSoft};";

    /// <summary>
    /// Identifies the logo configuration or policy value.
    /// </summary>
    public const string Logo = "display:block;width:40px;height:40px;";

    /// <summary>
    /// Identifies the brand configuration or policy value.
    /// </summary>
    public const string Brand =
        Font + "font-size:19px;font-weight:700;letter-spacing:-0.2px;"
        + $"color:{EmailBranding.TextBright};";

    /// <summary>
    /// Identifies the heading configuration or policy value.
    /// </summary>
    public const string Heading =
        Font + "margin:0 0 18px 0;font-size:24px;line-height:1.25;font-weight:700;"
        + $"letter-spacing:-0.4px;color:{EmailBranding.TextBright};";

    /// <summary>
    /// Identifies the footer brand configuration or policy value.
    /// </summary>
    public const string FooterBrand =
        Font + $"margin:0 0 6px 0;font-size:14px;font-weight:700;color:{EmailBranding.TextBright};";

    /// <summary>
    /// Identifies the footer tagline configuration or policy value.
    /// </summary>
    public const string FooterTagline =
        Font + $"margin:0 0 14px 0;font-size:13px;line-height:1.6;color:{EmailBranding.TextMuted};";

    /// <summary>
    /// Identifies the footer link wrap configuration or policy value.
    /// </summary>
    public const string FooterLinkWrap = Font + "margin:0 0 16px 0;font-size:13px;line-height:1.6;";

    /// <summary>
    /// Identifies the footer link configuration or policy value.
    /// </summary>
    public const string FooterLink =
        $"color:{EmailBranding.PrimaryInk};font-weight:700;text-decoration:underline;";

    /// <summary>
    /// Identifies the footer note configuration or policy value.
    /// </summary>
    public const string FooterNote =
        Font + $"margin:0;font-size:12px;line-height:1.6;color:{EmailBranding.TextDim};";

    /// <summary>
    /// Identifies the paragraph configuration or policy value.
    /// </summary>
    public const string Paragraph =
        Font + $"margin:0 0 16px 0;font-size:15px;line-height:1.65;color:{EmailBranding.Text};";

    /// <summary>
    /// Identifies the muted configuration or policy value.
    /// </summary>
    public const string Muted =
        Font + $"margin:0 0 16px 0;font-size:13px;line-height:1.6;color:{EmailBranding.TextMuted};";

    /// <summary>
    /// Identifies the button cell configuration or policy value.
    /// </summary>
    public const string ButtonCell =
        $"border-radius:10px;background-color:{EmailBranding.Primary};";

    /// <summary>
    /// Identifies the button link configuration or policy value.
    /// </summary>
    public const string ButtonLink =
        Font + "display:inline-block;padding:13px 30px;font-size:15px;font-weight:700;"
        + $"line-height:1.2;text-decoration:none;border-radius:10px;color:{EmailBranding.OnPrimary};";

    /// <summary>
    /// Identifies the one-time code cell configuration or policy value.
    /// </summary>
    public const string CodeCell =
        "padding:16px 28px;border-radius:10px;"
        + $"background-color:{EmailBranding.Panel};border:1px solid {EmailBranding.Border};";

    /// <summary>
    /// Identifies the one-time code text configuration or policy value.
    /// </summary>
    public const string CodeText =
        "font-family:Consolas,'Courier New',monospace;font-size:32px;font-weight:700;"
        + $"letter-spacing:8px;line-height:1.2;color:{EmailBranding.TextBright};";

    /// <summary>
    /// Identifies the fallback link configuration or policy value.
    /// </summary>
    public const string FallbackLink = $"color:{EmailBranding.PrimaryInk};word-break:break-all;";

    /// <summary>
    /// Identifies the panel cell configuration or policy value.
    /// </summary>
    public const string PanelCell =
        "padding:4px 18px;border-radius:10px;"
        + $"background-color:{EmailBranding.Panel};border:1px solid {EmailBranding.Border};";

    /// <summary>
    /// Identifies the callout cell configuration or policy value.
    /// </summary>
    public const string CalloutCell =
        "padding:14px 18px;border-radius:10px;"
        + $"background-color:{EmailBranding.Panel};border:1px solid {EmailBranding.Border};";

    /// <summary>
    /// Identifies the callout text configuration or policy value.
    /// </summary>
    public const string CalloutText =
        Font + $"margin:0;font-size:14px;line-height:1.6;color:{EmailBranding.Text};";

    /// <summary>
    /// Identifies the details label configuration or policy value.
    /// </summary>
    public const string DetailsLabel =
        Font + "padding:11px 16px 11px 0;font-size:13px;line-height:1.5;vertical-align:top;"
        + $"white-space:nowrap;color:{EmailBranding.TextMuted};";

    /// <summary>
    /// Identifies the details value configuration or policy value.
    /// </summary>
    public const string DetailsValue =
        Font + "padding:11px 0;font-size:14px;line-height:1.5;font-weight:600;"
        + $"vertical-align:top;color:{EmailBranding.Text};";

    /// <summary>
    /// Identifies the details row border configuration or policy value.
    /// </summary>
    public const string DetailsRowBorder = $"border-top:1px solid {EmailBranding.Border};";
}
