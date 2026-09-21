using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Runs the email outbox in the background: it delivers due messages as soon as this process stores
/// one and polls for the rest, which also picks up the work of other instances and the retries that
/// became due. A failed run is logged and never stops the loop or the host.
/// </summary>
/// <param name="deliverer">Component that claims and delivers one batch of due messages.</param>
/// <param name="signal">Signal raised when this process stores email.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class EmailOutboxProcessor(
    EmailOutboxDeliverer deliverer,
    EmailOutboxSignal signal,
    EmailQueueOptions options,
    ILogger<EmailOutboxProcessor> logger
) : BackgroundService
{
    /// <summary>
    /// Stops claiming new messages and waits for the deliveries in flight, up to the configured
    /// drain. Whatever is not delivered stays in the outbox for the next start.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drain.CancelAfter(options.ShutdownDrain);

        await base.StopAsync(drain.Token);

        if (ExecuteTask is { IsCompleted: false })
        {
            logger.EmailOutboxDrainIncomplete(options.ShutdownDrain);
        }
    }

    /// <summary>
    /// Delivers due email until shutdown, waiting for a signal or the poll interval between runs.
    /// Only the shutdown token ends the loop: a cancellation raised by a send timeout, a database
    /// command or any other dependency is a failed run like any other and the worker keeps going.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delivered = await DeliverSafelyAsync(stoppingToken);
            if (delivered >= options.BatchSize)
            {
                continue;
            }

            await WaitSafelyAsync(stoppingToken);
        }
    }

    private async Task<int> DeliverSafelyAsync(CancellationToken ct)
    {
        try
        {
            return await deliverer.DeliverDueAsync(ct);
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.EmailOutboxRunFailed(ex);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }

    private async Task WaitSafelyAsync(CancellationToken ct)
    {
        try
        {
            await signal.WaitAsync(options.PollInterval, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
    }
}
