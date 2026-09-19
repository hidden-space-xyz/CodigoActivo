using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Delivers one batch of due outbox messages: it removes whatever ran out of attempts, claims the due
/// rows with a lease, hands each one to the transport and then either removes the row or schedules the
/// next attempt. A message is attempted <see cref="EmailQueueOptions.MaxAttempts"/> times in total,
/// counted from the claim, and the row is removed on the last failure, so the table only ever holds
/// mail that still has a chance of leaving. The stored content shared by a batch is unprotected once
/// per run, shared by every recipient of it, and dropped when the run ends.
/// </summary>
/// <param name="store">Outbox rows this run works with.</param>
/// <param name="transport">The transport value.</param>
/// <param name="protector">Protector applied to the stored subject, bodies and attachments.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class EmailOutboxDeliverer(
    IEmailOutboxStore store,
    IEmailTransport transport,
    EmailOutboxProtector protector,
    EmailQueueOptions options,
    IClock clock,
    ILogger<EmailOutboxDeliverer> logger
)
{
    /// <summary>
    /// Claims and delivers the messages that are due, with the configured number of workers running
    /// concurrently, and clears the stored content that no message references any more. The content
    /// sweep also runs when nothing was due, because a process that died between the last delivery
    /// and the sweep leaves content behind with an empty outbox.
    /// </summary>
    /// <param name="ct">Cancellation token that stops further claiming.</param>
    /// <returns>A task whose result contains the number of claimed messages.</returns>
    public async Task<int> DeliverDueAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        await RemoveExhaustedAsync(now, ct);

        var claimed = await store.ClaimDueAsync(now, now + options.Lease, options.BatchSize, ct);
        if (claimed.Count > 0)
        {
            await DeliverAllAsync(claimed);
        }

        await SweepContentAsync();
        return claimed.Count;
    }

    private async Task DeliverAllAsync(IReadOnlyList<EmailOutboxMessage> claimed)
    {
        var payloads = claimed
            .DistinctBy(message => message.ContentId)
            .ToDictionary(
                message => message.ContentId,
                message => new Lazy<EmailOutboxPayload>(() => protector.Unprotect(message.Content))
            );

        try
        {
            await Parallel.ForEachAsync(
                claimed,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = options.Workers,
                    CancellationToken = CancellationToken.None,
                },
                async (message, _) => await DeliverAsync(message, payloads[message.ContentId])
            );
        }
        finally
        {
            payloads.Clear();
        }
    }

    private async Task DeliverAsync(EmailOutboxMessage message, Lazy<EmailOutboxPayload> payload)
    {
        try
        {
            var email = EmailOutboxProtector.ToMessage(message, payload.Value);
            using var timeout = new CancellationTokenSource(options.SendTimeout);
            await transport.SendAsync(email, timeout.Token);
            await store.RemoveAsync(message.Id, message.AttemptCount, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await RecordFailureAsync(message, ex);
        }
    }

    private async Task RecordFailureAsync(EmailOutboxMessage message, Exception failure)
    {
        var attempts = message.AttemptCount;

        try
        {
            if (EmailQueueOptions.IsExhausted(attempts))
            {
                await store.RemoveAsync(message.Id, attempts, CancellationToken.None);
                LogGaveUp(failure, message.Kind, attempts);
                return;
            }

            var nextAttemptAt = clock.UtcNow + EmailQueueOptions.RetryDelayAfter(attempts);
            await store.RescheduleAsync(
                message.Id,
                attempts,
                nextAttemptAt,
                Describe(failure),
                CancellationToken.None
            );
            logger.LogWarning(
                failure,
                "Attempt {Attempts} to deliver a {Kind} email failed; the next attempt is due at {NextAttemptAt}",
                attempts,
                message.Kind,
                nextAttemptAt
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Could not record the failed attempt {Attempts} of a {Kind} email; its lease will expire and it will be retried",
                attempts,
                message.Kind
            );
        }
    }

    private async Task RemoveExhaustedAsync(DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            foreach (var exhausted in await store.RemoveExhaustedAsync(now, ct))
            {
                LogGaveUp(failure: null, exhausted.Kind, exhausted.AttemptCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Could not remove the email whose delivery attempts are spent; the next run will retry"
            );
        }
    }

    private async Task SweepContentAsync()
    {
        try
        {
            await store.RemoveOrphanContentAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Could not clear the stored content of delivered email; the next run will retry"
            );
        }
    }

    private void LogGaveUp(Exception? failure, EmailKind kind, int attempts)
    {
        logger.LogError(
            failure,
            "Gave up on a {Kind} email after {Attempts} failed delivery attempts and removed it from the outbox",
            kind,
            attempts
        );
    }

    private static string Describe(Exception failure)
    {
        return $"{failure.GetType().Name}: {failure.Message}";
    }
}
