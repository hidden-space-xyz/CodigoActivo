using System.Threading.Channels;
using CodigoActivo.Domain.Communication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

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

    public bool TryEnqueue(EmailMessage message)
    {
        return channel.Writer.TryWrite(message);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        workers ??= Task.WhenAll(
            Enumerable
                .Range(0, options.Workers)
                .Select(_ => Task.Run(DeliverPendingAsync, CancellationToken.None))
        );
        return Task.CompletedTask;
    }

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
