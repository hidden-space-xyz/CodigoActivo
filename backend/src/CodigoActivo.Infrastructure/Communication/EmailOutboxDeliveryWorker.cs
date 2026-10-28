using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Runs the email outbox in the background on the one instance that holds the
/// <see cref="IEmailDeliveryLock"/>: it delivers due messages as soon as this process stores one and
/// polls for the rest, which also picks up the mail other instances store and the retries that
/// became due; a run that claims a full batch is followed at once by the next. An instance without
/// the lock tries to take it on every poll. A failed run never stops the loop or the host; it is
/// logged at most once per <see cref="EmailGuardOptions.AlertInterval"/>, since an unreachable
/// dependency fails every poll.
/// </summary>
/// <param name="deliverer">Component that claims and delivers one batch of due messages.</param>
/// <param name="deliveryLock">Lock that lets one instance deliver at a time.</param>
/// <param name="signal">Signal raised when this process stores email.</param>
/// <param name="options">Outbox policy: poll interval, batch size and shutdown drain.</param>
/// <param name="guard">Email guard configuration, whose alert interval spaces repeated failures.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class EmailOutboxDeliveryWorker(
    EmailOutboxDeliverer deliverer,
    IEmailDeliveryLock deliveryLock,
    EmailOutboxSignal signal,
    EmailQueueOptions options,
    EmailGuardOptions guard,
    IClock clock,
    ILogger<EmailOutboxDeliveryWorker> logger
) : BackgroundService
{
    private readonly LogThrottle runFailures = new(guard.AlertInterval);

    /// <summary>
    /// Stops claiming new messages and waits for the send in flight, up to
    /// <see cref="EmailQueueOptions.ShutdownDrain"/>, then releases the lock; the rest of the claimed
    /// batch goes back to the outbox untried. A message whose send is still running by then stays in
    /// the outbox and is claimed again once its lease expires.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drain.CancelAfter(options.ShutdownDrain);

        try
        {
            await base.StopAsync(drain.Token);
        }
        finally
        {
            await deliveryLock.DisposeAsync();
        }
    }

    /// <summary>
    /// Delivers due email until shutdown while this instance holds the lock, waiting for a signal or
    /// the poll interval between runs unless the last run claimed a full batch.
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
            return await deliveryLock.HoldAsync(ct) ? await deliverer.DeliverDueAsync(ct) : 0;
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            if (runFailures.TryEnter(clock.UtcNow))
            {
                logger.EmailOutboxRunFailed(ex);
            }

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
