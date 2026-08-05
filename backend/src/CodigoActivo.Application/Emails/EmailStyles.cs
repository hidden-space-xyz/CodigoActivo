namespace CodigoActivo.Application.Emails;

public static class EmailStyles
{
    private const string Font = $"font-family:{EmailBranding.FontStack};";

    public const string Body = $"margin:0;padding:0;background-color:{EmailBranding.Canvas};";

    public const string Canvas = $"background-color:{EmailBranding.Canvas};";

    public const string OuterCell = "padding:32px 12px;";

    public const string AccentBar =
        "height:4px;line-height:4px;font-size:0;border-radius:13px 13px 0 0;";

    public const string Card =
        "width:600px;max-width:600px;border-radius:14px;"
        + $"background-color:{EmailBranding.Surface};border:1px solid {EmailBranding.Border};";

    public const string Preheader =
        "display:none;max-height:0;max-width:0;opacity:0;overflow:hidden;"
        + $"mso-hide:all;font-size:1px;line-height:1px;color:{EmailBranding.Canvas};";

    public const string HeadCell =
        $"padding:24px 32px 20px 32px;border-bottom:1px solid {EmailBranding.BorderSoft};";

    public const string ContentCell = "padding:28px 32px 30px 32px;";

    public const string FooterCell =
        "padding:22px 32px 26px 32px;border-radius:0 0 13px 13px;"
        + $"background-color:{EmailBranding.Canvas};border-top:1px solid {EmailBranding.BorderSoft};";

    public const string Logo = "display:block;width:40px;height:40px;";

    public const string Brand =
        Font + "font-size:19px;font-weight:700;letter-spacing:-0.2px;"
        + $"color:{EmailBranding.TextBright};";

    public const string Heading =
        Font + "margin:0 0 18px 0;font-size:24px;line-height:1.25;font-weight:700;"
        + $"letter-spacing:-0.4px;color:{EmailBranding.TextBright};";

    public const string FooterBrand =
        Font + $"margin:0 0 6px 0;font-size:14px;font-weight:700;color:{EmailBranding.TextBright};";

    public const string FooterTagline =
        Font + $"margin:0 0 14px 0;font-size:13px;line-height:1.6;color:{EmailBranding.TextMuted};";

    public const string FooterLinkWrap = Font + "margin:0 0 16px 0;font-size:13px;line-height:1.6;";

    public const string FooterLink =
        $"color:{EmailBranding.PrimaryInk};font-weight:700;text-decoration:underline;";

    public const string FooterNote =
        Font + $"margin:0;font-size:12px;line-height:1.6;color:{EmailBranding.TextDim};";

    public const string Paragraph =
        Font + $"margin:0 0 16px 0;font-size:15px;line-height:1.65;color:{EmailBranding.Text};";

    public const string TightParagraph =
        Font + $"margin:0 0 10px 0;font-size:15px;line-height:1.65;color:{EmailBranding.Text};";

    public const string Muted =
        Font + $"margin:0 0 16px 0;font-size:13px;line-height:1.6;color:{EmailBranding.TextMuted};";

    public const string ButtonCell =
        $"border-radius:10px;background-color:{EmailBranding.Primary};";

    public const string ButtonLink =
        Font + "display:inline-block;padding:13px 30px;font-size:15px;font-weight:700;"
        + $"line-height:1.2;text-decoration:none;border-radius:10px;color:{EmailBranding.OnPrimary};";

    public const string FallbackLink = $"color:{EmailBranding.PrimaryInk};word-break:break-all;";

    public const string OtpCell =
        "padding:18px 16px;border-radius:10px;"
        + $"background-color:{EmailBranding.PrimarySoft};border:1px solid {EmailBranding.PrimaryEdge};";

    public const string OtpCode =
        $"font-family:{EmailBranding.MonoStack};font-size:26px;font-weight:700;"
        + $"letter-spacing:4px;color:{EmailBranding.PrimaryInk};";

    public const string PanelCell =
        "padding:4px 18px;border-radius:10px;"
        + $"background-color:{EmailBranding.Panel};border:1px solid {EmailBranding.Border};";

    public const string CalloutCell =
        "padding:14px 18px;border-radius:10px;"
        + $"background-color:{EmailBranding.Panel};border:1px solid {EmailBranding.Border};";

    public const string CalloutText =
        Font + $"margin:0;font-size:14px;line-height:1.6;color:{EmailBranding.Text};";

    public const string BulletMark =
        Font + $"padding:0 0 8px 0;font-size:15px;line-height:1.6;color:{EmailBranding.Primary};";

    public const string BulletText =
        Font + $"padding:0 0 8px 0;font-size:15px;line-height:1.6;color:{EmailBranding.Text};";

    public const string DetailsLabel =
        Font + "padding:11px 16px 11px 0;font-size:13px;line-height:1.5;vertical-align:top;"
        + $"white-space:nowrap;color:{EmailBranding.TextMuted};";

    public const string DetailsValue =
        Font + "padding:11px 0;font-size:14px;line-height:1.5;font-weight:600;"
        + $"vertical-align:top;color:{EmailBranding.Text};";

    public const string DetailsRowBorder = $"border-top:1px solid {EmailBranding.Border};";
}
