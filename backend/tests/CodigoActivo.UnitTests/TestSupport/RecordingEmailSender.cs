using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class RecordingEmailSender : IEmailTransport, IEmailSender
{
    private readonly List<EmailMessage> sent = [];

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

    public Exception? ThrowOnSend { get; set; }

    public Action? OnSend { get; set; }

    public ISet<string> FailingRecipients { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        if (FailingRecipients.Contains(message.ToAddress))
        {
            throw new InvalidOperationException($"Delivery to '{message.ToAddress}' failed.");
        }

        lock (sent)
        {
            sent.Add(message);
        }

        OnSend?.Invoke();
        return Task.CompletedTask;
    }
}
