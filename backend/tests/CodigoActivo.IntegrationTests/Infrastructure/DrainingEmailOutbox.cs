using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Stores email in the real outbox and delivers it inline with a deliverer of its own for every
/// drain, so a pause or a run of failures one test provokes never holds back the mail of the next.
/// Drains run one at a time, like the runs of the single delivery worker, so parallel requests
/// never deliver one message twice.
/// </summary>
public sealed class DrainingEmailOutbox(IEmailOutboxStore store, IServiceProvider services)
    : IEmailOutbox,
        IDisposable
{
    private readonly SemaphoreSlim drains = new(1, 1);

    public async Task<EmailEnqueueResult> TryEnqueueAsync(
        EmailBatch batch,
        CancellationToken ct = default
    )
    {
        var result = await store.TryEnqueueAsync(batch, ct);
        if (result is EmailEnqueueResult.Queued)
        {
            await DrainAsync(ct);
        }

        return result;
    }

    public async Task DrainAsync(CancellationToken ct)
    {
        await drains.WaitAsync(ct);
        try
        {
            var deliverer = services.GetRequiredService<EmailOutboxDeliverer>();
            while (await deliverer.DeliverDueAsync(ct) > 0) { }
        }
        finally
        {
            drains.Release();
        }
    }

    public void Dispose()
    {
        drains.Dispose();
    }
}
