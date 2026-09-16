using System.Threading.Channels;
using CodigoActivo.Domain.Communication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Dispatches channel email work through the configured queue.
/// </summary>
/// <param name="transport">The transport value.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ChannelEmailDispatcher(
    IEmailTransport transport,
    EmailQueueOptions options,
    ILogger<ChannelEmailDispatcher> logger
) : IHostedService, IEmailDispatcher
{
    private readonly Channel<EmailMessage> channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(options.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        }
    );

    private Task? workers;

    /// <summary>
    /// Attempts to add the email to the bounded delivery queue.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool TryEnqueue(EmailMessage message)
    {
        return channel.Writer.TryWrite(message);
    }

    /// <summary>
    /// Starts processing queued email in the background.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        workers ??= Task.WhenAll(
            Enumerable
                .Range(0, options.Workers)
                .Select(_ => Task.Run(DeliverPendingAsync, CancellationToken.None))
        );
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops queue processing after pending work has completed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        channel.Writer.TryComplete();

        if (workers is null)
        {
            return;
        }

        using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            drain.CancelAfter(options.ShutdownDrain);
            await workers.WaitAsync(drain.Token);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(
                ex,
                "The outbound email queue did not finish draining within {Drain}; {Pending} messages were left "
                    + "undelivered",
                options.ShutdownDrain,
                channel.Reader.Count
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "The outbound email queue failed while draining; {Pending} messages were left undelivered",
                channel.Reader.Count
            );
        }
    }

    private async Task DeliverPendingAsync()
    {
        await foreach (var message in channel.Reader.ReadAllAsync(CancellationToken.None))
        {
            await DeliverAsync(message);
        }
    }

    private async Task DeliverAsync(EmailMessage message)
    {
        try
        {
            using var timeout = new CancellationTokenSource(options.SendTimeout);
            await transport.SendAsync(message, timeout.Token);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(
                ex,
                "Delivery of a queued {Kind} message to {Recipient} timed out after {Timeout}",
                message.Kind,
                message.ToAddress,
                options.SendTimeout
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to deliver a queued {Kind} message to {Recipient}",
                message.Kind,
                message.ToAddress
            );
        }
    }
}
