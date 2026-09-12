using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

public sealed class ThrottledEmailSender(
    IEmailDispatcher dispatcher,
    EmailGuardOptions options,
    EmailQueueOptions queueOptions,
    IClock clock,
    ILogger<ThrottledEmailSender> logger
) : IEmailSender
{
    private readonly EmailSendLimiter limiter = new(options, clock);

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var decision = limiter.TryConsume(message.Kind, message.ToAddress);
        Report(decision);

        if (decision.Scope is not EmailLimitScope.None)
        {
            throw new EmailRateLimitedException(decision.Scope);
        }

        if (!dispatcher.TryEnqueue(message))
        {
            logger.LogError(
                "The outbound email queue is full at {Capacity} messages; an email was held back",
                queueOptions.Capacity
            );
            throw new EmailRateLimitedException(EmailLimitScope.Global);
        }

        return Task.CompletedTask;
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
