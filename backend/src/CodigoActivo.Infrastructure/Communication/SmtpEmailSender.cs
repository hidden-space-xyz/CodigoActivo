using CodigoActivo.Domain.Communication;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Sends email through the configured smtp transport.
/// </summary>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger)
    : IEmailTransport
{
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
        using var mime = BuildMime(message);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    /// <summary>
    /// Sends the many message to its recipients.
    /// </summary>
    /// <param name="messages">Email messages to deliver as a batch.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an email batch.</returns>
    public async Task<EmailBatchResult> SendManyAsync(
        IReadOnlyList<EmailMessage> messages,
        CancellationToken ct = default
    )
    {
        if (messages.Count is 0)
        {
            return new EmailBatchResult(0, 0);
        }

        EnsureConfigured();

        var sent = 0;
        var failed = 0;
        var completed = false;

        using var client = new SmtpClient();
        await ConnectAsync(client, ct);
        try
        {
            foreach (var message in messages)
            {
                ct.ThrowIfCancellationRequested();
                var connectionDropped = false;
                try
                {
                    using var mime = BuildMime(message);
                    await client.SendAsync(mime, ct);
                    sent++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    logger.LogError(
                        ex,
                        "Failed to send an email to {Recipient}",
                        message.ToAddress
                    );
                    connectionDropped = !client.IsConnected;
                }

                if (connectionDropped)
                {
                    var unattempted = messages.Count - sent - failed;
                    failed += unattempted;
                    logger.LogError(
                        "The SMTP connection dropped mid-batch after {Sent} of {Total} messages; {Unattempted} were never attempted",
                        sent,
                        messages.Count,
                        unattempted
                    );
                    break;
                }
            }

            completed = true;
        }
        finally
        {
            if (!completed)
            {
                logger.LogWarning(
                    "A batch send stopped early after {Sent} sent and {Failed} failed of {Total} messages",
                    sent,
                    failed,
                    messages.Count
                );
            }

            if (client.IsConnected)
            {
                await client.DisconnectAsync(quit: true, CancellationToken.None);
            }
        }

        return new EmailBatchResult(sent, failed);
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

    private static ContentType ParseContentType(string value)
    {
        return
            ContentType.TryParse(value, out var parsed)
            && !string.Equals(parsed.MediaType, "message", StringComparison.OrdinalIgnoreCase)
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
