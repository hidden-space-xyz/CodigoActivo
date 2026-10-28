using System.Net.Sockets;
using CodigoActivo.Application.Abstractions.Email;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Sends email straight to the configured SMTP server, over one connection per session that every
/// message of the session reuses. Failures leave as <see cref="EmailDeliveryException"/> with whose
/// fault they were: the outbox deliverer decides whether a message is dropped, retried or the whole
/// delivery pauses. The size limit the server announces is recorded in
/// <see cref="SmtpServerLimits"/>. A message with a one-click unsubscribe address carries the
/// <c>List-Unsubscribe</c> and <c>List-Unsubscribe-Post</c> headers of RFC 8058, which the DKIM
/// signature of the relay must cover.
/// </summary>
/// <param name="options">SMTP server, encryption, credentials and sender of every message.</param>
/// <param name="serverLimits">Limits the server announces, updated on every connection.</param>
public sealed class SmtpEmailSender(SmtpOptions options, SmtpServerLimits serverLimits)
    : IEmailTransport
{
    private readonly SmtpOptions options = options;
    private readonly SmtpServerLimits serverLimits = serverLimits;

    private const string OneClickUnsubscribePost = "List-Unsubscribe=One-Click";
    private const int MessageTooLarge = 552;
    private const int ServiceNotAvailable = 421;

    private static readonly string[] CompositeMediaTypes = ["message", "multipart"];

    /// <summary>
    /// Connects and authenticates when <see cref="SmtpOptions.Username"/> is set, without retrying.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the open session.</returns>
    /// <exception cref="EmailDeliveryException">
    /// The server is not configured, cannot be reached or refused the login, with
    /// <see cref="EmailDeliveryFailure.ServerUnavailable"/>.
    /// </exception>
    public async Task<IEmailTransportSession> OpenAsync(CancellationToken ct = default)
    {
        var session = new Session(this);
        try
        {
            await session.ConnectAsync(ct);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    private void EnsureConfigured()
    {
        if (
            string.IsNullOrWhiteSpace(options.Host)
            || string.IsNullOrWhiteSpace(options.FromAddress)
        )
        {
            throw new EmailDeliveryException(
                EmailDeliveryFailure.ServerUnavailable,
                "The SMTP server is not configured (SMTP_HOST and SMTP_FROM_ADDRESS).",
                null
            );
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

    /// <summary>
    /// Tells whose fault a command the server rejected was, from the command and its status: a
    /// recipient whose address or mailbox is refused for good (enhanced status 5.1 or 5.2) or that
    /// is refused for now is the recipient's, a message too large is the message's, a refused sender
    /// or a closed service (421) is the server's, and anything else, such as a recipient refused by
    /// the relay's policy or quota or a message deferred or refused after its content, may pass.
    /// </summary>
    /// <param name="exception">Rejection reported by MailKit.</param>
    /// <returns>The failure, whose message keeps the codes but not the reply of the server.</returns>
    private static EmailDeliveryException Classify(SmtpCommandException exception)
    {
        var status = (int)exception.StatusCode;
        var failure = (exception.ErrorCode, status) switch
        {
            (_, ServiceNotAvailable) => EmailDeliveryFailure.ServerUnavailable,
            (SmtpErrorCode.SenderNotAccepted, _) => EmailDeliveryFailure.ServerUnavailable,
            (SmtpErrorCode.RecipientNotAccepted, >= 500)
                when RefusesTheAddress(exception.Message) => EmailDeliveryFailure.RecipientRejected,
            (SmtpErrorCode.RecipientNotAccepted, >= 500) => EmailDeliveryFailure.Transient,
            (SmtpErrorCode.RecipientNotAccepted, _) => EmailDeliveryFailure.RecipientDeferred,
            (SmtpErrorCode.MessageNotAccepted, MessageTooLarge) =>
                EmailDeliveryFailure.MessageRejected,
            _ => EmailDeliveryFailure.Transient,
        };

        return new EmailDeliveryException(
            failure,
            $"The SMTP server rejected the message: {exception.ErrorCode} ({status})",
            null
        );
    }

    private static bool RefusesTheAddress(string reply)
    {
        var enhanced = reply.TrimStart();
        return enhanced.StartsWith("5.1.", StringComparison.Ordinal)
            || enhanced.StartsWith("5.2.", StringComparison.Ordinal);
    }

    private static bool IsConnectionFailure(Exception exception)
    {
        return exception
            is IOException
                or SocketException
                or SmtpProtocolException
                or ServiceNotConnectedException
                or ServiceNotAuthenticatedException
                or AuthenticationException
                or SslHandshakeException;
    }

    /// <summary>
    /// Keeps one SMTP connection for a run of the deliverer. A connection that already carried a
    /// message is checked with <c>NOOP</c> before the next one and opened again when the server
    /// closed it without a word, before anything of the message is sent.
    /// </summary>
    /// <param name="sender">Sender whose configuration the session uses.</param>
    private sealed class Session(SmtpEmailSender sender) : IEmailTransportSession
    {
        private readonly SmtpClient client = new();

        private bool used;

        /// <inheritdoc />
        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (used && client.IsConnected && !await RespondsAsync(ct))
            {
                await DisconnectQuietlyAsync();
            }

            if (!client.IsConnected)
            {
                await ConnectAsync(ct);
            }

            used = true;
            using var mime = sender.BuildMime(message);
            try
            {
                await client.SendAsync(mime, ct);
            }
            catch (SmtpCommandException ex)
            {
                throw Classify(ex);
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                await DisconnectQuietlyAsync();
                throw new EmailDeliveryException(
                    EmailDeliveryFailure.Transient,
                    "The SMTP server did not accept the message in time.",
                    ex
                );
            }
            catch (Exception ex) when (IsConnectionFailure(ex))
            {
                await DisconnectQuietlyAsync();
                throw new EmailDeliveryException(
                    EmailDeliveryFailure.ServerUnavailable,
                    "The connection to the SMTP server failed while sending.",
                    ex
                );
            }
        }

        /// <summary>
        /// Connects, authenticates when a username is set and records the size limit the server
        /// announces.
        /// </summary>
        /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="EmailDeliveryException">
        /// The connection or the login failed, with
        /// <see cref="EmailDeliveryFailure.ServerUnavailable"/>.
        /// </exception>
        public async Task ConnectAsync(CancellationToken ct)
        {
            sender.EnsureConfigured();
            var options = sender.options;
            try
            {
                await client.ConnectAsync(
                    options.Host,
                    options.Port,
                    MapSecurity(options.Security),
                    ct
                );
                if (!string.IsNullOrEmpty(options.Username))
                {
                    await client.AuthenticateAsync(options.Username, options.Password, ct);
                }

                sender.serverLimits.Record(client.MaxSize);
                used = false;
            }
            catch (Exception ex) when (ex is not EmailDeliveryException)
            {
                await DisconnectQuietlyAsync();
                throw new EmailDeliveryException(
                    EmailDeliveryFailure.ServerUnavailable,
                    $"Could not open a session with the SMTP server: {ex.GetType().Name}",
                    ex is SmtpCommandException ? null : ex
                );
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DisconnectQuietlyAsync();
            client.Dispose();
        }

        private async Task<bool> RespondsAsync(CancellationToken ct)
        {
            try
            {
                await client.NoOpAsync(ct);
                return true;
            }
            catch (Exception ex) when (ex is SmtpCommandException || IsConnectionFailure(ex))
            {
                return false;
            }
        }

        private async Task DisconnectQuietlyAsync()
        {
            if (!client.IsConnected)
            {
                return;
            }

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await client.DisconnectAsync(quit: true, timeout.Token);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return;
            }
        }
    }
}
