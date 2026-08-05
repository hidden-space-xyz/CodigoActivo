using System.Text.RegularExpressions;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed partial class RecordingEmailSender : IEmailTransport, IEmailSender
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

    public ISet<string> FailingRecipients { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public int Batches { get; private set; }

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

        return Task.CompletedTask;
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
            var delivered = messages.Where(m => !FailingRecipients.Contains(m.ToAddress)).ToList();
            sent.AddRange(delivered);
            return Task.FromResult(
                new EmailBatchResult(delivered.Count, messages.Count - delivered.Count)
            );
        }
    }

    public string LastCode()
    {
        EmailMessage last;
        lock (sent)
        {
            last = sent[^1];
        }

        var match = CodePattern.Match(last.TextBody);
        return !match.Success
            ? throw new InvalidOperationException(
                "The last email does not contain a verification code."
            )
            : match.Groups["code"].Value;
    }

    [GeneratedRegex(@"[?&]code=(?<code>[^\s&]+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodePattern { get; }
}
