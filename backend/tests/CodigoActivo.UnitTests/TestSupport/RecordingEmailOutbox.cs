using CodigoActivo.Domain.Communication;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class RecordingEmailOutbox : IEmailOutbox
{
    private readonly List<EmailBatch> batches = [];

    public IReadOnlyList<EmailBatch> Batches
    {
        get
        {
            lock (batches)
            {
                return [.. batches];
            }
        }
    }

    public IReadOnlyList<EmailMessage> Messages
    {
        get
        {
            lock (batches)
            {
                return [.. batches.SelectMany(batch => batch.ToMessages())];
            }
        }
    }

    public bool RejectAll { get; set; }

    public Exception? ThrowOnEnqueue { get; set; }

    public Task<bool> TryEnqueueAsync(EmailBatch batch, CancellationToken ct = default)
    {
        if (ThrowOnEnqueue is not null)
        {
            throw ThrowOnEnqueue;
        }

        if (RejectAll)
        {
            return Task.FromResult(false);
        }

        lock (batches)
        {
            batches.Add(batch);
        }

        return Task.FromResult(true);
    }
}
