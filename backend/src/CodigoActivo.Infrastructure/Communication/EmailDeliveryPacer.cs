using CodigoActivo.Application.Abstractions.Time;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Paces the outbox delivery in memory: a token bucket holds the messages the SMTP provider accepts
/// per minute, and every recipient of a notice or administrator-written mail has the hourly and
/// daily budgets of <see cref="EmailGuardOptions"/>, so actions repeated in a loop wait instead of
/// flooding a mailbox or spending the budget of the provider. Only one instance delivers at a time,
/// so the pace is the pace of the whole application.
/// </summary>
/// <param name="smtp">Messages the SMTP provider accepts per minute.</param>
/// <param name="guard">Per-recipient budgets and the bounds of their tracking.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class EmailDeliveryPacer(SmtpOptions smtp, EmailGuardOptions guard, IClock clock)
{
    private const double MinutesPerHour = 60;

    private readonly Lock gate = new();
    private readonly RecipientBudgets recipients = new(guard, clock.UtcNow);
    private readonly int perMinute = Math.Max(smtp.MaxPerMinute, 1);

    private TokenBucket provider = TokenBucket.Full(Math.Max(smtp.MaxPerMinute, 1), clock.UtcNow);

    /// <summary>
    /// Gets how many messages the provider accepts right now.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The whole messages the per-minute budget holds.</returns>
    public int Available(DateTimeOffset now)
    {
        lock (gate)
        {
            provider = provider.Refill(now, perMinute, perMinute * MinutesPerHour);
            return (int)Math.Floor(provider.Tokens);
        }
    }

    /// <summary>
    /// Spends one message of the per-minute budget, even when it is already spent: the claim only took
    /// what <see cref="Available"/> allowed.
    /// </summary>
    /// <param name="now">Current time.</param>
    public void Spend(DateTimeOffset now)
    {
        lock (gate)
        {
            provider = provider.Refill(now, perMinute, perMinute * MinutesPerHour).Consume();
        }
    }

    /// <summary>
    /// Spends one message of the budgets of a recipient when they allow it.
    /// </summary>
    /// <param name="address">Recipient address.</param>
    /// <param name="now">Current time.</param>
    /// <returns>Whether the message may go now and, if not, when it may.</returns>
    public RecipientSpend TrySpendRecipient(string address, DateTimeOffset now)
    {
        return recipients.TrySpend(EmailSendLimiter.NormalizeKey(address), now);
    }

    /// <summary>
    /// Gives back to the budgets of a recipient the message the server did not take.
    /// </summary>
    /// <param name="address">Recipient address.</param>
    /// <param name="now">Current time.</param>
    public void RefundRecipient(string address, DateTimeOffset now)
    {
        recipients.Refund(EmailSendLimiter.NormalizeKey(address), now);
    }
}
