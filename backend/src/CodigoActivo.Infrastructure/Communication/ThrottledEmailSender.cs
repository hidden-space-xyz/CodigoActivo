using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

public sealed class ThrottledEmailSender(
    IEmailTransport transport,
    EmailGuardOptions options,
    IClock clock,
    ILogger<ThrottledEmailSender> logger
) : IEmailSender
{
    private readonly EmailSendLimiter limiter = new(options, clock);

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var decision = limiter.TryConsume(message.Kind, message.ToAddress);
        Report(decision, message);

        if (decision.Scope != EmailLimitScope.None)
            throw new EmailRateLimitedException(decision.Scope);

        return transport.SendAsync(message, ct);
    }

    private void Report(EmailSendDecision decision, EmailMessage message)
    {
        switch (decision.Alert)
        {
            case EmailGuardAlert.RecipientThrottled:
                logger.LogWarning(
                    "The outbound email quota is now holding mail for {Recipient}; a {Kind} message was not sent",
                    message.ToAddress,
                    message.Kind
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
                    "The global outbound email budget is exhausted and a {Kind} message to {Recipient} was denied; automatic mail is held until the budget refills, admin-written email is unaffected",
                    message.Kind,
                    message.ToAddress
                );
                break;
            case EmailGuardAlert.TrackingSaturated:
                logger.LogWarning(
                    "The outbound email quota already tracks {Limit} recipients, so new addresses are accounted against the global budget only",
                    options.MaxTrackedRecipients
                );
                break;
            case EmailGuardAlert.None:
            default:
                break;
        }
    }
}
