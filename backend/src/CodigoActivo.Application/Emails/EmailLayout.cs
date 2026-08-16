using System.Net;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public sealed record EmailBlock(string Html, string Text);

public sealed record EmailContent(
    string Html,
    string Text,
    IReadOnlyList<EmailInlineImage> InlineImages
);

public sealed record EmailDocument(
    string Heading,
    string Preheader,
    string? RecipientName,
    string SiteUrl,
    EmailAccent Accent,
    string FooterNote
);

public static class EmailLayout
{
    private const string TextRule =
        "------------------------------------------------------------";

    private const string BlockSeparator = "\n\n";

    private const string PreheaderSpacer =
        "&#8203;&#847;&#8203;&#847;&#8203;&#847;&#8203;&#847;&#8203;&#847;&#8203;&#847;";

    private const string TableAttributes =
        "role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"";

    public static EmailContent Render(EmailDocument document, IReadOnlyList<EmailBlock> blocks)
    {
        var kept = blocks.Where(block => !string.IsNullOrWhiteSpace(block.Text)).ToList();
        return new EmailContent(
            Html(document, kept),
            Text(document, kept),
            EmailBranding.InlineImages
        );
    }

    private static string Text(EmailDocument document, List<EmailBlock> blocks)
    {
        var sections = new List<string>(blocks.Count + 3)
        {
            $"{AppStrings.EmailsSharedBrandName}\n{document.Heading}\n{TextRule}",
        };

        if (document.RecipientName is not null)
        {
            sections.Add(AppStrings.EmailsSharedGreeting(document.RecipientName));
        }

        sections.AddRange(blocks.Select(block => block.Text.Trim()));
        sections.Add(
            $"{TextRule}\n{AppStrings.EmailsFooterTagline}\n{document.SiteUrl}\n{document.FooterNote}"
        );

        return string.Join(BlockSeparator, sections);
    }

    private static string Html(EmailDocument document, List<EmailBlock> blocks)
    {
        var heading = WebUtility.HtmlEncode(document.Heading);
        var preheader = WebUtility.HtmlEncode(document.Preheader);
        var body = string.Concat(blocks.Select(block => block.Html));
        var accentBar = $"{EmailStyles.AccentBar}background-color:{document.Accent.Line};";
        var greeting =
            document.RecipientName is null ? string.Empty
            : EmailBlocks.Paragraph(
                AppStrings.EmailsSharedGreeting(WebUtility.HtmlEncode(document.RecipientName))
            );

        return $$"""
            <!DOCTYPE html>
            <html lang="es" xmlns="http://www.w3.org/1999/xhtml" xmlns:o="urn:schemas-microsoft-com:office:office">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta http-equiv="X-UA-Compatible" content="IE=edge">
            <meta name="color-scheme" content="light dark">
            <meta name="supported-color-schemes" content="light dark">
            <title>{{heading}}</title>
            <!--[if mso]><xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml><![endif]-->
            <style>
            body {
              margin: 0;
              padding: 0;
              width: 100% !important;
              -webkit-text-size-adjust: 100%;
              -ms-text-size-adjust: 100%;
            }
            table { border-collapse: collapse; mso-table-lspace: 0; mso-table-rspace: 0; }
            img { border: 0; line-height: 100%; outline: none; text-decoration: none; }
            @media only screen and (max-width: 620px) {
              .ca-shell { width: 100% !important; }
              .ca-pad { padding-left: 20px !important; padding-right: 20px !important; }
              .ca-h1 { font-size: 21px !important; }
            }
            @media (prefers-color-scheme: dark) {
              .ca-canvas { background-color: #101014 !important; }
              .ca-card { background-color: #1a1a1f !important; border-color: #2a2a31 !important; }
              .ca-head { border-bottom-color: #2a2a31 !important; }
              .ca-footer {
                background-color: #161619 !important;
                border-top-color: #2a2a31 !important;
              }
              .ca-panel { background-color: #131317 !important; border-color: #2a2a31 !important; }
              .ca-callout {
                background-color: #131317 !important;
                border-top-color: #2a2a31 !important;
                border-right-color: #2a2a31 !important;
                border-bottom-color: #2a2a31 !important;
              }
              .ca-row { border-top-color: #2a2a31 !important; }
              .ca-brand, .ca-h1 { color: #f3f4f6 !important; }
              .ca-text, .ca-value { color: #e6e7ea !important; }
              .ca-muted, .ca-label { color: #9a9ba1 !important; }
              .ca-link { color: #f9a320 !important; }
            }
            </style>
            </head>
            <body class="ca-canvas" style="{{EmailStyles.Body}}">
            <div style="{{EmailStyles.Preheader}}">{{preheader}}{{PreheaderSpacer}}</div>
            <table {{TableAttributes}} class="ca-canvas" width="100%" style="{{EmailStyles.Canvas}}">
            <tr>
            <td align="center" style="{{EmailStyles.OuterCell}}">
            <table {{TableAttributes}} class="ca-shell ca-card" width="600" style="{{EmailStyles.Card}}">
            <tr>
            <td height="4" style="{{accentBar}}">&nbsp;</td>
            </tr>
            <tr>
            <td class="ca-pad ca-head" style="{{EmailStyles.HeadCell}}">
            {{Header()}}
            </td>
            </tr>
            <tr>
            <td class="ca-pad" style="{{EmailStyles.ContentCell}}">
            <h1 class="ca-h1" style="{{EmailStyles.Heading}}">{{heading}}</h1>
            {{greeting}}{{body}}
            </td>
            </tr>
            <tr>
            <td class="ca-pad ca-footer" style="{{EmailStyles.FooterCell}}">
            {{Footer(document)}}
            </td>
            </tr>
            </table>
            </td>
            </tr>
            </table>
            </body>
            </html>
            """;
    }

    private static string Header()
    {
        var logoAlt = WebUtility.HtmlEncode(AppStrings.EmailsSharedLogoAlt);
        var brand = WebUtility.HtmlEncode(AppStrings.EmailsSharedBrandName);
        var logo = $"cid:{EmailBranding.LogoContentId}";

        return $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0">
            <tr>
            <td width="40" valign="middle" style="padding-right:12px;">
            <img src="{logo}" width="40" height="40" alt="{logoAlt}" style="{EmailStyles.Logo}">
            </td>
            <td valign="middle">
            <span class="ca-brand" style="{EmailStyles.Brand}">{brand}</span>
            </td>
            </tr>
            </table>
            """;
    }

    private static string Footer(EmailDocument document)
    {
        var brand = WebUtility.HtmlEncode(AppStrings.EmailsSharedBrandName);
        var tagline = WebUtility.HtmlEncode(AppStrings.EmailsFooterTagline);
        var website = WebUtility.HtmlEncode(AppStrings.EmailsFooterWebsiteLabel);
        var siteUrl = WebUtility.HtmlEncode(document.SiteUrl);
        var note = WebUtility.HtmlEncode(document.FooterNote);

        return $"""
            <p class="ca-brand" style="{EmailStyles.FooterBrand}">{brand}</p>
            <p class="ca-muted" style="{EmailStyles.FooterTagline}">{tagline}</p>
            <p style="{EmailStyles.FooterLinkWrap}">
            <a class="ca-link" href="{siteUrl}" style="{EmailStyles.FooterLink}">{website}</a>
            </p>
            <p class="ca-muted" style="{EmailStyles.FooterNote}">{note}</p>
            """;
    }
}
