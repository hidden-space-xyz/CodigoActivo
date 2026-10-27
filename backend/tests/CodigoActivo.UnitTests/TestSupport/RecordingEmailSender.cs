using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class RecordingEmailSender : IEmailTransport, IEmailSender
{
    private readonly List<EmailMessage> sent = [];
    private int opened;

    public IReadOnlyList<EmailMessage> Sent
    {
        get
        {
            lock (sent)
            {
                return [.. sent];
            }
        }
    }

    public int Opened => Volatile.Read(ref opened);

    public Exception? ThrowOnOpen { get; set; }

    public Exception? ThrowOnSend { get; set; }

    public Action? OnSend { get; set; }

    public ISet<string> FailingRecipients { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ISet<string> RejectedRecipients { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public Task<IEmailTransportSession> OpenAsync(CancellationToken ct = default)
    {
        if (ThrowOnOpen is not null)
        {
            throw ThrowOnOpen;
        }

        Interlocked.Increment(ref opened);
        return Task.FromResult<IEmailTransportSession>(new Session(this));
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        if (FailingRecipients.Contains(message.ToAddress))
        {
            throw new EmailDeliveryException(
                EmailDeliveryFailure.RecipientDeferred,
                "The SMTP server rejected the message: RecipientNotAccepted (450)",
                null
            );
        }

        if (RejectedRecipients.Contains(message.ToAddress))
        {
            throw new EmailDeliveryException(
                EmailDeliveryFailure.RecipientRejected,
                "The SMTP server rejected the message: RecipientNotAccepted (550)",
                null
            );
        }

        lock (sent)
        {
            sent.Add(message);
        }

        OnSend?.Invoke();
        return Task.CompletedTask;
    }

    private sealed class Session(RecordingEmailSender sender) : IEmailTransportSession
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            return sender.SendAsync(message, ct);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
