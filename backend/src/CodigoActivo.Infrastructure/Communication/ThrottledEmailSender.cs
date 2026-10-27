using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Applies the email guard to automatic mail and stores what it accepts in the outbox, which the
/// background worker delivers; nothing reaches the SMTP server here. The guard is an
/// <see cref="EmailSendLimiter"/> held by this singleton, so its budgets belong to the process.
/// </summary>
/// <param name="outbox">Outbox the accepted message is stored in for background delivery.</param>
/// <param name="options">Email guard limits applied before a message is stored.</param>
/// <param name="queueOptions">Outbox policy, read for the capacity a full outbox reports.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ThrottledEmailSender(
    IEmailOutbox outbox,
    EmailGuardOptions options,
    EmailQueueOptions queueOptions,
    IClock clock,
    ILogger<ThrottledEmailSender> logger
) : IEmailSender
{
    private readonly EmailSendLimiter limiter = new(options, clock);

    /// <summary>
    /// Spends the quota of the message and stores it in the outbox, so the task completes once the
    /// row is committed, not once the SMTP server accepts it. The quota is spent before the message
    /// is stored and is not refunded when the outbox refuses it. An exhausted global budget and a
    /// saturated recipient tracking are logged at most once per
    /// <see cref="EmailGuardOptions.AlertInterval"/>; a full outbox is logged every time.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EmailRateLimitedException">
    /// The guard denied the message, with the scope that denied it, or the outbox is full, with
    /// <see cref="EmailLimitScope.Global"/>; nothing was stored.
    /// </exception>
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var decision = limiter.TryConsume(message.Kind, message.ToAddress);
        Report(decision);

        if (decision.Scope is not EmailLimitScope.None)
        {
            throw new EmailRateLimitedException(decision.Scope);
        }

        if (!await outbox.TryEnqueueAsync(EmailBatch.ForOne(message), ct))
        {
            logger.EmailOutboxFull(queueOptions.Capacity);
            throw new EmailRateLimitedException(EmailLimitScope.Global);
        }
    }

    private void Report(EmailSendDecision decision)
    {
        switch (decision.Alert)
        {
            case EmailGuardAlert.GlobalBudgetExhausted:
                logger.GlobalEmailBudgetExhausted();
                break;
            case EmailGuardAlert.TrackingSaturated:
                logger.EmailRecipientTrackingSaturated(options.MaxTrackedRecipients);
                break;
            case EmailGuardAlert.None:
            default:
                break;
        }
    }
}
