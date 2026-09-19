using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class DrainingEmailOutbox(IEmailOutboxStore store, EmailOutboxDeliverer deliverer)
    : IEmailOutbox
{
    public async Task<bool> TryEnqueueAsync(EmailBatch batch, CancellationToken ct = default)
    {
        if (!await store.TryEnqueueAsync(batch, ct))
        {
            return false;
        }

        await DrainAsync(ct);
        return true;
    }

    public async Task DrainAsync(CancellationToken ct)
    {
        while (await deliverer.DeliverDueAsync(ct) > 0) { }
    }
}
