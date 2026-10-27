using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Applies the email guard to the codes and stores automatic mail in the outbox, which the
/// background worker delivers; nothing reaches the SMTP server here. Notices are never refused:
/// their topics bound what waits, and the deliverer paces them. The guard is an
/// <see cref="EmailSendLimiter"/> held by this singleton, so its budgets belong to the process.
/// </summary>
/// <param name="outbox">Outbox the accepted message is stored in for background delivery.</param>
/// <param name="options">Email guard limits applied before a code is stored.</param>
/// <param name="smtp">SMTP provider pace, which caps the budget of mail anyone can ask for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ThrottledEmailSender(
    IEmailOutbox outbox,
    EmailGuardOptions options,
    SmtpOptions smtp,
    IClock clock,
    ILogger<ThrottledEmailSender> logger
) : IEmailSender
{
    private readonly EmailSendLimiter limiter = new(options, smtp, clock);

    /// <summary>
    /// Spends the quota of a code and stores the message in the outbox, so the task completes once
    /// the row is committed, not once the SMTP server accepts it. The quota is spent before the
    /// message is stored and is not refunded. An exhausted anonymous budget and a saturated
    /// recipient tracking are logged at most once per <see cref="EmailGuardOptions.AlertInterval"/>.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EmailRateLimitedException">
    /// The guard denied a code, with the scope that denied it; nothing was stored.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The outbox refused the message, which only a message larger than the SMTP server accepts
    /// can cause; nothing was stored.
    /// </exception>
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (EmailQueueOptions.IsGuarded(message.Kind))
        {
            var decision = limiter.TryConsume(message.Kind, message.ToAddress);
            Report(decision);

            if (decision.Scope is not EmailLimitScope.None)
            {
                throw new EmailRateLimitedException(decision.Scope);
            }
        }

        var result = await outbox.TryEnqueueAsync(EmailBatch.ForOne(message), ct);
        if (result is not EmailEnqueueResult.Queued)
        {
            throw new InvalidOperationException(
                $"The outbox refused an automatic {message.Kind} email: {result}."
            );
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
