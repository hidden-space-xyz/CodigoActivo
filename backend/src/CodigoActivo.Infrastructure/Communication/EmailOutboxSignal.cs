using System.Threading.Channels;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Wakes the delivery worker of this process as soon as email is stored, so a login code leaves
/// without waiting for the next poll. Missing the signal only costs latency: the worker looks for
/// due messages on every poll anyway, and other instances rely on their own polling.
/// </summary>
public sealed class EmailOutboxSignal
{
    private readonly Channel<byte> pending = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = false,
            SingleWriter = false,
        }
    );

    /// <summary>
    /// Reports that at least one message is waiting to be delivered.
    /// </summary>
    public void Notify()
    {
        pending.Writer.TryWrite(0);
    }

    /// <summary>
    /// Waits for a notification, giving up after the supplied delay. Notifications collapse: however
    /// many arrive while nobody is waiting, they release exactly one wait.
    /// </summary>
    /// <param name="timeout">Longest time to wait before returning.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when a notification was consumed;
    /// <see langword="false"/> when the wait gave up after the supplied delay.
    /// </returns>
    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);

        try
        {
            await pending.Reader.ReadAsync(deadline.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
