using System.Security.Cryptography;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Delivers one batch of due outbox messages over one SMTP session. A run removes what expired,
/// claims what the pace of the SMTP provider and its daily limit allow, by priority, and sends it
/// message by message: a delivered or refused-for-good message is removed, a message whose recipient
/// already got their share waits without spending an attempt, a failure that may pass is retried
/// with a growing delay, and a server that cannot take mail pauses the whole delivery, handing the
/// rest of the batch back untried. Three failures in a row whose cause is unknown are blamed on the
/// server too; a recipient the server defers never is. Repeated
/// failures are logged at most once per <see cref="EmailGuardOptions.AlertInterval"/>. Delivery is
/// at least once: a sent message whose row cannot be removed is attempted again.
/// </summary>
/// <param name="store">Outbox rows this run works with.</param>
/// <param name="transport">Transport each run opens one session of.</param>
/// <param name="protector">Protector applied to the stored subject, bodies and attachments.</param>
/// <param name="pacer">Pace of the SMTP provider and of every recipient.</param>
/// <param name="options">Outbox policy: batch size, lease, send timeout and schedules.</param>
/// <param name="smtp">Daily limit of the SMTP provider.</param>
/// <param name="guard">Email guard configuration, whose alert interval spaces repeated failures.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class EmailOutboxDeliverer(
    IEmailOutboxStore store,
    IEmailTransport transport,
    EmailOutboxProtector protector,
    EmailDeliveryPacer pacer,
    EmailQueueOptions options,
    SmtpOptions smtp,
    EmailGuardOptions guard,
    IClock clock,
    ILogger<EmailOutboxDeliverer> logger
)
{
    private readonly Lock gate = new();
    private readonly LogThrottle attemptFailures = new(guard.AlertInterval);
    private readonly LogThrottle expiredRemovalFailures = new(guard.AlertInterval);
    private readonly LogThrottle pauses = new(guard.AlertInterval);
    private readonly LogThrottle dailyLimitAlerts = new(guard.AlertInterval);

    private DateTimeOffset pausedUntil = DateTimeOffset.MinValue;
    private DateTimeOffset nextOverdueCheckAt = DateTimeOffset.MinValue;
    private int serverFailures;
    private int failuresInARow;

    /// <summary>
    /// Gets the time until which the delivery is paused because the SMTP server could not take mail;
    /// a time in the past when it is not paused.
    /// </summary>
    public DateTimeOffset PausedUntil
    {
        get
        {
            lock (gate)
            {
                return pausedUntil;
            }
        }
    }

    /// <summary>
    /// Removes the expired messages, claims and delivers the messages that are due and allowed, and
    /// clears the stored content that no message references any more. Nothing is claimed while the
    /// delivery is paused. Only a failed claim or a cancellation reaches the caller: the failures of
    /// single messages, of the removal, of the count and of the sweep are logged here.
    /// </summary>
    /// <param name="ct">
    /// Cancellation token that stops the removal and the claim; a claimed batch is still delivered
    /// to the end, each send bounded by <see cref="EmailQueueOptions.SendTimeout"/>.
    /// </param>
    /// <returns>A task whose result contains the number of claimed messages.</returns>
    public async Task<int> DeliverDueAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        await RemoveExpiredAsync(now, ct);
        await ReportOverdueAsync(now, ct);

        var claimed = now < PausedUntil ? [] : await ClaimAsync(now, ct);
        if (claimed.Count > 0)
        {
            await DeliverAllAsync(claimed);
        }

        await SweepContentAsync();
        return claimed.Count;
    }

    private async Task<IReadOnlyList<EmailOutboxMessage>> ClaimAsync(
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var slots = Math.Min(options.BatchSize, pacer.Available(now));
        if (slots <= 0)
        {
            return [];
        }

        var sent = await store.CountSentSinceAsync(now - EmailQueueOptions.DailyWindow, ct);
        var claimable = EmailQueueOptions.ClaimablePriority(sent, smtp.MaxPerDay);
        if (claimable is not EmailQueueOptions.BulkPriority && dailyLimitAlerts.TryEnter(now))
        {
            logger.EmailDailyLimitReached(smtp.MaxPerDay);
        }

        return claimable is { } maxPriority
            ? await store.ClaimDueAsync(now, now + options.Lease, slots, maxPriority, ct)
            : [];
    }

    private async Task DeliverAllAsync(IReadOnlyList<EmailOutboxMessage> claimed)
    {
        var payloads = claimed
            .DistinctBy(message => message.ContentId)
            .ToDictionary(
                message => message.ContentId,
                message => new Lazy<EmailOutboxPayload>(() => protector.Unprotect(message.Content))
            );
        var run = new DeliveryRun();

        try
        {
            for (var index = 0; index < claimed.Count; index++)
            {
                var message = claimed[index];
                var outcome = await DeliverAsync(message, payloads[message.ContentId], run);
                if (outcome is not Outcome.Continue)
                {
                    var untried = outcome is Outcome.PauseBeforeThis ? index : index + 1;
                    await HandBackAsync(claimed.Skip(untried));
                    return;
                }
            }
        }
        finally
        {
            if (run.Session is not null)
            {
                await run.Session.DisposeAsync();
            }

            await RecordSentAsync(run.Sent);
            payloads.Clear();
        }
    }

    private async Task<Outcome> DeliverAsync(
        EmailOutboxMessage message,
        Lazy<EmailOutboxPayload> payload,
        DeliveryRun run
    )
    {
        var now = clock.UtcNow;
        if (EmailQueueOptions.IsPacedPerRecipient(message.Priority))
        {
            var spend = pacer.TrySpendRecipient(message.ToAddress, now);
            if (!spend.Allowed)
            {
                await ReleaseAsync(message, now + spend.RetryAfter);
                return Outcome.Continue;
            }
        }

        EmailMessage email;
        try
        {
            email = EmailOutboxProtector.ToMessage(message, payload.Value);
        }
        catch (CryptographicException ex)
        {
            await DropAsync(message, EmailDeliveryFailure.MessageRejected, ex);
            return Outcome.Continue;
        }

        using var timeout = new CancellationTokenSource(options.SendTimeout);
        try
        {
            run.Session ??= await transport.OpenAsync(timeout.Token);
            pacer.Spend(now);
            run.Sent++;
            await run.Session.SendAsync(email, timeout.Token);
            Succeeded();
            await RemoveDeliveredAsync(message);
            return Outcome.Continue;
        }
        catch (EmailDeliveryException ex)
            when (ex.Failure is EmailDeliveryFailure.ServerUnavailable)
        {
            Pause(ex);
            return Outcome.PauseBeforeThis;
        }
        catch (EmailDeliveryException ex)
            when (ex.Failure
                    is EmailDeliveryFailure.RecipientRejected
                        or EmailDeliveryFailure.MessageRejected
            )
        {
            await DropAsync(message, ex.Failure, ex);
            return Outcome.Continue;
        }
        catch (EmailDeliveryException ex)
            when (ex.Failure is EmailDeliveryFailure.RecipientDeferred)
        {
            await RetryLaterAsync(message, ex);
            return Outcome.Continue;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RetryLaterAsync(message, ex);
            if (FailedInARow() < EmailQueueOptions.FailuresBeforePause)
            {
                return Outcome.Continue;
            }

            Pause(ex);
            return Outcome.PauseAfterThis;
        }
    }

    private void Succeeded()
    {
        lock (gate)
        {
            serverFailures = 0;
            failuresInARow = 0;
        }
    }

    private int FailedInARow()
    {
        lock (gate)
        {
            return ++failuresInARow;
        }
    }

    private void Pause(Exception cause)
    {
        TimeSpan delay;
        DateTimeOffset now;
        lock (gate)
        {
            serverFailures++;
            failuresInARow = 0;
            delay = EmailQueueOptions.PauseDelayAfter(serverFailures);
            now = clock.UtcNow;
            pausedUntil = now + delay;
        }

        if (pauses.TryEnter(now))
        {
            logger.EmailDeliveryPaused((int)delay.TotalMinutes, cause);
        }
    }

    private async Task HandBackAsync(IEnumerable<EmailOutboxMessage> untried)
    {
        var resumeAt = PausedUntil;
        foreach (var message in untried)
        {
            await ReleaseAsync(message, resumeAt);
        }
    }

    private async Task ReleaseAsync(EmailOutboxMessage message, DateTimeOffset nextAttemptAt)
    {
        try
        {
            await store.ReleaseAsync(
                message.Id,
                message.AttemptCount,
                nextAttemptAt,
                CancellationToken.None
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailAttemptNotRecorded(message.AttemptCount, message.Kind, ex);
        }
    }

    private async Task RemoveDeliveredAsync(EmailOutboxMessage message)
    {
        try
        {
            await store.RemoveAsync(message.Id, message.AttemptCount, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailAttemptNotRecorded(message.AttemptCount, message.Kind, ex);
        }
    }

    private async Task DropAsync(
        EmailOutboxMessage message,
        EmailDeliveryFailure failure,
        Exception cause
    )
    {
        logger.EmailRejected(message.Kind, failure, cause);
        await RemoveDeliveredAsync(message);
    }

    private async Task RetryLaterAsync(EmailOutboxMessage message, Exception failure)
    {
        var attempts = message.AttemptCount;
        try
        {
            var now = clock.UtcNow;
            await store.RescheduleAsync(
                message.Id,
                attempts,
                now + EmailQueueOptions.RetryDelayAfter(attempts),
                Describe(failure),
                CancellationToken.None
            );
            if (attemptFailures.TryEnter(now))
            {
                logger.EmailDeliveryAttemptFailed(attempts, message.Kind, failure);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailAttemptNotRecorded(attempts, message.Kind, ex);
        }
    }

    private async Task RemoveExpiredAsync(DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            var expired = await store.RemoveExpiredAsync(now, ct);
            foreach (var kind in expired.GroupBy(kind => kind))
            {
                logger.EmailsExpired(kind.Count(), kind.Key);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (expiredRemovalFailures.TryEnter(now))
            {
                logger.ExpiredEmailNotRemoved(ex);
            }
        }
    }

    private async Task ReportOverdueAsync(DateTimeOffset now, CancellationToken ct)
    {
        lock (gate)
        {
            if (now < nextOverdueCheckAt)
            {
                return;
            }

            nextOverdueCheckAt = now + guard.AlertInterval;
        }

        var overdue = await store.CountOverdueAsync(now - EmailQueueOptions.OverdueAge, ct);
        if (overdue > 0)
        {
            logger.EmailsOverdue(overdue, (int)EmailQueueOptions.OverdueAge.TotalHours);
        }
    }

    private async Task RecordSentAsync(int sent)
    {
        if (sent is 0)
        {
            return;
        }

        try
        {
            await store.RecordSentAsync(clock.UtcNow, sent, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailDeliveryCountNotRecorded(ex);
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
            logger.EmailContentSweepFailed(ex);
        }
    }

    private static string Describe(Exception failure)
    {
        return $"{failure.GetType().Name}: {failure.Message}";
    }

    private enum Outcome
    {
        Continue,
        PauseBeforeThis,
        PauseAfterThis,
    }

    private sealed class DeliveryRun
    {
        /// <summary>
        /// Gets or sets the session the run opened with its first send, if any.
        /// </summary>
        public IEmailTransportSession? Session { get; set; }

        /// <summary>
        /// Gets or sets how many messages the run handed to the SMTP server.
        /// </summary>
        public int Sent { get; set; }
    }
}
