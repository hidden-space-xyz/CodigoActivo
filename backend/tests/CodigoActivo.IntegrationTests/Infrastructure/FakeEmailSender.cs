using System.Text.RegularExpressions;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed partial class FakeEmailSender : IEmailTransport, IEmailDispatcher
{
    private readonly List<EmailMessage> sent = [];
    private readonly HashSet<string> failingRecipients = new(StringComparer.OrdinalIgnoreCase);

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

    public int Batches { get; private set; }

    public Exception? ThrowOnSend { get; set; }

    public void FailFor(params string[] addresses)
    {
        lock (sent)
        {
            failingRecipients.UnionWith(addresses);
        }
    }

    public bool TryEnqueue(EmailMessage message)
    {
        Record(message);
        return true;
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        Record(message);
        return Task.CompletedTask;
    }

    private void Record(EmailMessage message)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        lock (sent)
        {
            sent.Add(message);
        }
    }

    public Task<EmailBatchResult> SendManyAsync(
        IReadOnlyList<EmailMessage> messages,
        CancellationToken ct = default
    )
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        lock (sent)
        {
            Batches++;
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
            Batches = 0;
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
        {
            throw new InvalidOperationException($"No email was sent to '{address}'.");
        }

        var match = OtpPattern.Match(message.TextBody);
        return !match.Success
            ? throw new InvalidOperationException(
                $"The email sent to '{address}' does not contain a verification code."
            )
            : match.Groups["code"].Value;
    }

    [GeneratedRegex(@"[?&]code=(?<code>[^\s&]+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OtpPattern { get; }
}
