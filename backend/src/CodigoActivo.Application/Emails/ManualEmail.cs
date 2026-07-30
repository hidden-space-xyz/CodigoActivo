using System.Net;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public sealed record ManualEmailContent(string Subject, string HtmlBody, string TextBody);

public static class ManualEmail
{
    private const string Signature = "Este mensaje te lo envía el equipo de Código Activo.";

    public static ManualEmailContent Render(string subject, string body)
    {
        var textBody = $"""
            {body}

            --
            {Signature}
            """;

        var htmlBody = $"""
            <div style="font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;">
              <h2 style="color: #111827;">{WebUtility.HtmlEncode(subject)}</h2>
              {ToParagraphs(body)}
              <p style="color: #6b7280; font-size: 13px;">{Signature}</p>
            </div>
            """;

        return new ManualEmailContent(subject, htmlBody, textBody);
    }

    public static EmailMessage Create(
        ManualEmailContent content,
        string toAddress,
        string toName,
        IReadOnlyList<EmailAttachment> attachments
    )
    {
        return new EmailMessage(
            toAddress,
            toName,
            content.Subject,
            content.HtmlBody,
            content.TextBody,
            attachments
        );
    }

    private static string ToParagraphs(string body)
    {
        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var blocks = normalized.Split(
            "\n\n",
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );

        return string.Concat(
            blocks.Select(block =>
                $"<p>{WebUtility.HtmlEncode(block).Replace("\n", "<br>", StringComparison.Ordinal)}</p>"
            )
        );
    }
}
