using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
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
            logger.LogError(
                "The outbound email outbox is full at {Capacity} pending messages; an email was held back",
                queueOptions.Capacity
            );
            throw new EmailRateLimitedException(EmailLimitScope.Global);
        }
    }

    private void Report(EmailSendDecision decision)
    {
        switch (decision.Alert)
        {
            case EmailGuardAlert.RecipientThrottled:
                logger.LogWarning(
                    "The per-recipient outbound email quota is now holding mail; an email was not sent"
                );
                break;
            case EmailGuardAlert.GlobalBudgetLow:
                logger.LogWarning(
                    "The global outbound email budget is running low with {Remaining} messages left before automatic mail is held",
                    decision.GlobalRemaining
                );
                break;
            case EmailGuardAlert.GlobalBudgetExhausted:
                logger.LogError(
                    "The global outbound email budget is exhausted and an email was denied; automatic mail is held "
                        + "until the budget refills, admin-written email is unaffected"
                );
                break;
            case EmailGuardAlert.TrackingSaturated:
                logger.LogWarning(
                    "The outbound email quota already tracks {Limit} recipients, so new addresses are accounted against the global budget "
                        + "only",
                    options.MaxTrackedRecipients
                );
                break;
            case EmailGuardAlert.None:
            default:
                break;
        }
    }
}
