using System.Net;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Represents an email block value used by the application.
/// </summary>
/// <param name="Html">The html value.</param>
/// <param name="Text">The text value.</param>
public sealed record EmailBlock(string Html, string Text);

/// <summary>
/// Represents an email content value used by the application.
/// </summary>
/// <param name="Html">The html value.</param>
/// <param name="Text">The text value.</param>
/// <param name="InlineImages">The inline images value.</param>
public sealed record EmailContent(
    string Html,
    string Text,
    IReadOnlyList<EmailInlineImage> InlineImages
);

/// <summary>
/// Parses and validates email documents.
/// </summary>
/// <param name="Heading">The heading value.</param>
/// <param name="Preheader">The preheader value.</param>
/// <param name="RecipientName">Name the message greets; a null or empty name greets nobody.</param>
/// <param name="SiteUrl">The site url value.</param>
/// <param name="Accent">The accent value.</param>
/// <param name="NewsContact">
/// Contact address of a news email, whose footer carries it and leaves a place for the
/// unsubscribe link of each recipient; <see langword="null"/> for any other email.
/// </param>
public sealed record EmailDocument(
    string Heading,
    string Preheader,
    string? RecipientName,
    string SiteUrl,
    EmailAccent Accent,
    string? NewsContact = null
);

/// <summary>
/// Wraps transactional email content in the shared branded layout.
/// </summary>
public static class EmailLayout
{
    private const string TextRule = "------------------------------------------------------------";

    private const string UnsubscribeSlot = "{{unsubscribe-url}}";

    private const string BlockSeparator = "\n\n";

    private const string PreheaderSpacer =
        "&#8203;&#847;&#8203;&#847;&#8203;&#847;&#8203;&#847;&#8203;&#847;&#8203;&#847;";

    private const string TableAttributes =
        "role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"";

    /// <summary>
    /// Builds the render output.
    /// </summary>
    /// <param name="document">The document value.</param>
    /// <param name="blocks">The blocks value.</param>
    /// <returns>The resulting email content value.</returns>
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
            $"{Organization.Name}\n{document.Heading}\n{TextRule}",
        };

        if (!string.IsNullOrEmpty(document.RecipientName))
        {
            sections.Add(AppStrings.EmailsSharedGreeting(document.RecipientName));
        }

        sections.AddRange(blocks.Select(block => block.Text.Trim()));
        var footer = $"{AppStrings.EmailsFooterTagline}\n{document.SiteUrl}";
        sections.Add(
            document.NewsContact is { } contact
                ? $"{TextRule}\n{NewsNoticeText(contact)}{BlockSeparator}{footer}"
                : $"{TextRule}\n{footer}"
        );

        return string.Join(BlockSeparator, sections);
    }

    /// <summary>
    /// Puts the unsubscribe link of one recipient in the place the footer of a news email
    /// leaves for it.
    /// </summary>
    /// <param name="body">Rendered body shared by every recipient of the email.</param>
    /// <param name="unsubscribeUrl">
    /// Unsubscribe link of the recipient; <see langword="null"/> leaves the body as it is.
    /// </param>
    /// <param name="html">Whether the body is HTML, where the link is encoded.</param>
    /// <returns>The body of the recipient.</returns>
    public static string LinkUnsubscribe(string body, string? unsubscribeUrl, bool html)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (unsubscribeUrl is null)
        {
            return body;
        }

        return body.Replace(
            UnsubscribeSlot,
            html ? WebUtility.HtmlEncode(unsubscribeUrl) : unsubscribeUrl,
            StringComparison.Ordinal
        );
    }

    private static string NewsNoticeText(string contact)
    {
        return $"{AppStrings.EmailsFooterNewsNotice}\n"
            + $"{AppStrings.EmailsFooterUnsubscribeText(UnsubscribeSlot)}\n"
            + AppStrings.EmailsFooterContact(contact);
    }

    private static string Html(EmailDocument document, List<EmailBlock> blocks)
    {
        var heading = WebUtility.HtmlEncode(document.Heading);
        var preheader = WebUtility.HtmlEncode(document.Preheader);
        var body = string.Concat(blocks.Select(block => block.Html));
        var accentBar = $"{EmailStyles.AccentBar}background-color:{document.Accent.Line};";
        var greeting = string.IsNullOrEmpty(document.RecipientName)
            ? string.Empty
            : EmailBlocks.Paragraph(
                AppStrings.EmailsSharedGreeting(WebUtility.HtmlEncode(document.RecipientName))
            );

        return $$"""
            <!DOCTYPE html>
            <html lang="es" xmlns="http://www.w3.org/1999/xhtml" xmlns:o="urn:schemas-microsoft-com:office:office">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
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
        var brand = WebUtility.HtmlEncode(Organization.Name);
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
        var brand = WebUtility.HtmlEncode(Organization.Name);
        var tagline = WebUtility.HtmlEncode(AppStrings.EmailsFooterTagline);
        var website = WebUtility.HtmlEncode(AppStrings.EmailsFooterWebsiteLabel);
        var siteUrl = WebUtility.HtmlEncode(document.SiteUrl);

        return $"""
            <p class="ca-brand" style="{EmailStyles.FooterBrand}">{brand}</p>
            <p class="ca-muted" style="{EmailStyles.FooterTagline}">{tagline}</p>
            <p style="{EmailStyles.FooterLinkWrap}">
            <a class="ca-link" href="{siteUrl}" style="{EmailStyles.FooterLink}">{website}</a>
            </p>
            {(
                document.NewsContact is { } contact
                    ? NewsNoticeHtml(contact)
                    : string.Empty
            )}
            """;
    }

    private static string NewsNoticeHtml(string contactAddress)
    {
        var notice = WebUtility.HtmlEncode(AppStrings.EmailsFooterNewsNotice);
        var unsubscribe = WebUtility.HtmlEncode(AppStrings.EmailsFooterUnsubscribeLabel);
        var address = WebUtility.HtmlEncode(contactAddress);
        var contact = AppStrings.EmailsFooterContact(
            $"<a class=\"ca-link\" href=\"mailto:{address}\" style=\"{EmailStyles.FooterLink}\">{address}</a>"
        );

        return $"""
            <p class="ca-muted" style="{EmailStyles.FooterNotice}">{notice}
            <a class="ca-link" href="{UnsubscribeSlot}" style="{EmailStyles.FooterLink}">{unsubscribe}</a></p>
            <p class="ca-muted" style="{EmailStyles.FooterNotice}">{contact}</p>
            """;
    }
}
