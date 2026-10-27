using CodigoActivo.Application.Abstractions.Email;

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

    public EmailEnqueueResult Answer { get; set; } = EmailEnqueueResult.Queued;

    public Exception? ThrowOnEnqueue { get; set; }

    public Task<EmailEnqueueResult> TryEnqueueAsync(
        EmailBatch batch,
        CancellationToken ct = default
    )
    {
        if (ThrowOnEnqueue is not null)
        {
            throw ThrowOnEnqueue;
        }

        if (Answer is EmailEnqueueResult.Queued)
        {
            lock (batches)
            {
                batches.Add(batch);
            }
        }

        return Task.FromResult(Answer);
    }
}
