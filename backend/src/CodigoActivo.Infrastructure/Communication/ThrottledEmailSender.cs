using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Sends email through the configured throttled transport.
/// </summary>
/// <param name="outbox">Outbox the accepted message is stored in for background delivery.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="queueOptions">The queue options value.</param>
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
    /// Sends the prepared throttled email sender message.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
            case EmailGuardAlert.RecipientThrottled:
                logger.RecipientEmailQuotaReached();
                break;
            case EmailGuardAlert.GlobalBudgetLow:
                logger.GlobalEmailBudgetLow(decision.GlobalRemaining);
                break;
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
