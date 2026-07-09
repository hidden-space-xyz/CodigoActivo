using CodigoActivo.Domain.Communication;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CodigoActivo.Infrastructure.Communication;

public sealed class SmtpEmailSender(SmtpOptions options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new InvalidOperationException("The SMTP host is not configured (SMTP_HOST).");
        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            throw new InvalidOperationException(
                "The SMTP sender address is not configured (SMTP_FROM_ADDRESS)."
            );
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host, options.Port, MapSecurity(options.Security), ct);
        if (!string.IsNullOrEmpty(options.Username))
            await client.AuthenticateAsync(options.Username, options.Password, ct);

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(quit: true, ct);
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
