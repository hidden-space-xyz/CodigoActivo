using CodigoActivo.Application.Abstractions.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Sends email through the configured smtp transport. Failures leave as exceptions: the outbox
/// delivery worker decides whether they are retried and records them. A message with a one-click
/// unsubscribe address carries the <c>List-Unsubscribe</c> and <c>List-Unsubscribe-Post</c>
/// headers of RFC 8058, which the DKIM signature of the relay must cover.
/// </summary>
/// <param name="options">Configuration values used by the component.</param>
public sealed class SmtpEmailSender(SmtpOptions options) : IEmailTransport
{
    private const string OneClickUnsubscribePost = "List-Unsubscribe=One-Click";

    private static readonly string[] CompositeMediaTypes = ["message", "multipart"];

    /// <summary>
    /// Sends the prepared smtp email sender message.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        EnsureConfigured();

        using var client = new SmtpClient();
        await ConnectAsync(client, ct);
        await DeliverAsync(client, message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new InvalidOperationException("The SMTP host is not configured (SMTP_HOST).");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            throw new InvalidOperationException(
                "The SMTP sender address is not configured (SMTP_FROM_ADDRESS)."
            );
        }
    }

    private async Task DeliverAsync(SmtpClient client, EmailMessage message, CancellationToken ct)
    {
        using var mime = BuildMime(message);
        try
        {
            await client.SendAsync(mime, ct);
        }
        catch (SmtpCommandException ex)
        {
            throw new SmtpCommandException(
                ex.ErrorCode,
                ex.StatusCode,
                $"The SMTP server rejected the message: {ex.ErrorCode} ({ex.StatusCode})"
            );
        }
    }

    private async Task ConnectAsync(SmtpClient client, CancellationToken ct)
    {
        await client.ConnectAsync(options.Host, options.Port, MapSecurity(options.Security), ct);
        if (!string.IsNullOrEmpty(options.Username))
        {
            await client.AuthenticateAsync(options.Username, options.Password, ct);
        }
    }

    private MimeMessage BuildMime(EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        AddOneClickUnsubscribe(mime, message.OneClickUnsubscribeUrl);

        var builder = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody };

        foreach (var image in message.InlineImages ?? [])
        {
            var resource = builder.LinkedResources.Add(
                image.FileName,
                image.Content,
                ParseContentType(image.ContentType)
            );
            resource.ContentId = image.ContentId;
            resource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline)
            {
                FileName = image.FileName,
            };
        }

        foreach (var attachment in message.Attachments ?? [])
        {
            builder.Attachments.Add(
                attachment.FileName,
                attachment.Content,
                ParseContentType(attachment.ContentType)
            );
        }

        mime.Body = builder.ToMessageBody();
        return mime;
    }

    private static void AddOneClickUnsubscribe(MimeMessage mime, string? url)
    {
        if (url is null)
        {
            return;
        }

        mime.Headers.Add(HeaderId.ListUnsubscribe, $"<{url}>");
        mime.Headers.Add(HeaderId.ListUnsubscribePost, OneClickUnsubscribePost);
    }

    private static ContentType ParseContentType(string value)
    {
        return
            ContentType.TryParse(value, out var parsed)
            && !CompositeMediaTypes.Contains(parsed.MediaType, StringComparer.OrdinalIgnoreCase)
            ? parsed
            : new ContentType("application", "octet-stream");
    }

    private static SecureSocketOptions MapSecurity(SmtpSecurityMode mode)
    {
        return mode switch
        {
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SmtpSecurityMode.None => SecureSocketOptions.None,
            SmtpSecurityMode.Auto => SecureSocketOptions.Auto,
            _ => SecureSocketOptions.StartTls,
        };
    }
}
