using System.Text.RegularExpressions;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed partial class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> sent = [];
    private int batches;

    private readonly HashSet<string> failingRecipients = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<EmailMessage> Sent => sent;

    public int Batches => batches;

    public Exception? ThrowOnSend { get; set; }

    public void FailFor(params string[] addresses)
    {
        lock (sent)
        {
            failingRecipients.UnionWith(addresses);
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (ThrowOnSend is not null)
            throw ThrowOnSend;

        lock (sent)
        {
            sent.Add(message);
        }

        return Task.CompletedTask;
    }

    public Task<EmailBatchResult> SendManyAsync(
        IReadOnlyList<EmailMessage> messages,
        CancellationToken ct = default
    )
    {
        if (ThrowOnSend is not null)
            throw ThrowOnSend;

        lock (sent)
        {
            batches++;
            var delivered = messages
                .Where(m => !failingRecipients.Contains(m.ToAddress))
                .ToList();
            sent.AddRange(delivered);
            return Task.FromResult(
                new EmailBatchResult(delivered.Count, messages.Count - delivered.Count)
            );
        }
    }

    public void Clear()
    {
        lock (sent)
        {
            batches = 0;
            sent.Clear();
            failingRecipients.Clear();
            ThrowOnSend = null;
        }
    }

    public string LastOtpSentTo(string address)
    {
        EmailMessage? message;
        lock (sent)
        {
            message = sent.LastOrDefault(m =>
                string.Equals(m.ToAddress, address, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (message is null)
            throw new InvalidOperationException($"No email was sent to '{address}'.");

        var match = OtpPattern().Match(message.TextBody);
        return !match.Success
            ? throw new InvalidOperationException(
                $"The email sent to '{address}' does not contain a verification code."
            )
            : match.Groups[1].Value;
    }

    [GeneratedRegex(@"[?&]code=([^\s&]+)")]
    private static partial Regex OtpPattern();
}
