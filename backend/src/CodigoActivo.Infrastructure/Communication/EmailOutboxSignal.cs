using System.Diagnostics.CodeAnalysis;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Wakes the delivery worker of this process as soon as email is stored, so a login code leaves
/// without waiting for the next poll. Missing the signal only costs latency: the worker looks for
/// due messages on every poll anyway, and other instances rely on their own polling.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The semaphore never exposes AvailableWaitHandle, so it holds no unmanaged resource; the signal is a singleton that must stay usable for the whole process lifetime."
)]
public sealed class EmailOutboxSignal
{
    private readonly SemaphoreSlim pending = new(0, 1);
    private readonly Lock gate = new();

    /// <summary>
    /// Reports that at least one message is waiting to be delivered. Never blocks and never throws:
    /// a notification that finds one already pending is dropped instead of queueing up.
    /// </summary>
    public void Notify()
    {
        lock (gate)
        {
            if (pending.CurrentCount is 0)
            {
                pending.Release();
            }
        }
    }

    /// <summary>
    /// Waits for a notification, giving up after the supplied delay. Notifications collapse: however
    /// many arrive while nobody is waiting, they release exactly one wait. Letting the delay elapse
    /// is an ordinary outcome and raises no exception; only the caller's own cancellation does.
    /// </summary>
    /// <param name="timeout">Longest time to wait before returning.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when a notification was consumed;
    /// <see langword="false"/> when the wait gave up after the supplied delay.
    /// </returns>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        return pending.WaitAsync(timeout, ct);
    }
}
