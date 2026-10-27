using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Infrastructure.Diagnostics;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Represents the outcome of <see cref="EmailSendLimiter.TryConsume"/>.
/// </summary>
/// <param name="Scope">
/// Limit that denied the message; <see cref="EmailLimitScope.None"/> when it may be stored.
/// </param>
/// <param name="Alert">
/// Condition the caller has to log now, which may accompany an accepted message;
/// <see cref="EmailGuardAlert.None"/> when there is nothing to log.
/// </param>
public readonly record struct EmailSendDecision(EmailLimitScope Scope, EmailGuardAlert Alert);

/// <summary>
/// Enforces the email guard on the codes, the only mail limited before it is stored: an hourly and a
/// daily budget per normalized address, and a budget shared by the mail anyone can ask for, the
/// verification, email-in-use and password-reset mail, which second-factor codes never spend.
/// Second-factor codes also have address budgets of their own, so flooding an address with mail
/// anyone can ask for never stops its owner from receiving a login code. The budgets live in memory
/// behind a lock.
/// </summary>
/// <param name="options">Limits applied, and the interval that spaces repeated alerts.</param>
/// <param name="smtp">SMTP provider pace, which caps the anonymous budget at half of it.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class EmailSendLimiter(EmailGuardOptions options, SmtpOptions smtp, IClock clock)
{
    private const string TwoFactorPrefix = "2fa:";

    private readonly Lock gate = new();
    private readonly RecipientBudgets recipients = new(options, clock.UtcNow);
    private readonly (int Burst, int PerHour) anonymousBudget = options.AnonymousBudget(
        smtp.MaxPerMinute
    );

    private readonly LogThrottle anonymousExhaustedAlerts = new(options.AlertInterval);
    private readonly LogThrottle saturationAlerts = new(options.AlertInterval);

    private TokenBucket anonymous = TokenBucket.Full(
        options.AnonymousBudget(smtp.MaxPerMinute).Burst,
        clock.UtcNow
    );

    /// <summary>
    /// Gets how many address budgets the limiter holds; an address that received second-factor codes
    /// and other codes counts twice.
    /// </summary>
    public int TrackedRecipients => recipients.Count;

    /// <summary>
    /// Checks the anonymous budget, for the kinds that spend it, and then the hourly and daily
    /// budgets of the address, and spends one message from each when all of them allow it. A denial
    /// spends nothing. A new address while <see cref="EmailGuardOptions.MaxTrackedRecipients"/>
    /// budgets are held spends the anonymous budget only.
    /// </summary>
    /// <param name="kind">Email category whose limits are applied.</param>
    /// <param name="address">Recipient address, reduced with <see cref="NormalizeKey"/>.</param>
    /// <returns>
    /// The decision, with <see cref="EmailLimitScope.None"/> when the message was counted, or the
    /// scope that denied it, and the alert to log, if any.
    /// </returns>
    public EmailSendDecision TryConsume(EmailKind kind, string address)
    {
        var now = clock.UtcNow;
        var twoFactor = kind is EmailKind.TwoFactorCode;
        var key = (twoFactor ? TwoFactorPrefix : string.Empty) + NormalizeKey(address);

        lock (gate)
        {
            if (!twoFactor)
            {
                anonymous = anonymous.Refill(now, anonymousBudget.Burst, anonymousBudget.PerHour);
                if (anonymous.Tokens < 1)
                {
                    return new EmailSendDecision(
                        EmailLimitScope.Global,
                        anonymousExhaustedAlerts.TryEnter(now)
                            ? EmailGuardAlert.GlobalBudgetExhausted
                            : EmailGuardAlert.None
                    );
                }
            }

            var spend = recipients.TrySpend(key, now);
            if (!spend.Allowed)
            {
                return new EmailSendDecision(EmailLimitScope.Recipient, EmailGuardAlert.None);
            }

            if (!twoFactor)
            {
                anonymous = anonymous.Consume();
            }

            return new EmailSendDecision(
                EmailLimitScope.None,
                spend.Saturated && saturationAlerts.TryEnter(now)
                    ? EmailGuardAlert.TrackingSaturated
                    : EmailGuardAlert.None
            );
        }
    }

    /// <summary>
    /// Reduces an address to the key of its mailbox, so its aliases share one budget: trimmed and
    /// lower-cased, without a <c>+tag</c> in the local part and, on <c>gmail.com</c> and
    /// <c>googlemail.com</c>, without its dots. A value with nothing before its last <c>@</c> is only
    /// trimmed and lower-cased.
    /// </summary>
    /// <param name="address">Recipient address used as a rate-limit key.</param>
    /// <returns>The normalized key.</returns>
    public static string NormalizeKey(string address)
    {
        var trimmed = address.Trim().ToLowerInvariant();
        var at = trimmed.LastIndexOf('@');
        if (at <= 0)
        {
            return trimmed;
        }

        var local = trimmed[..at];
        var domain = trimmed[(at + 1)..];

        var plus = local.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            local = local[..plus];
        }

        if (FoldsDots(domain))
        {
            local = local.Replace(".", string.Empty, StringComparison.Ordinal);
        }

        return string.Concat(local, "@", domain);
    }

    private static bool FoldsDots(string domain)
    {
        return string.Equals(domain, "gmail.com", StringComparison.Ordinal)
            || string.Equals(domain, "googlemail.com", StringComparison.Ordinal);
    }
}
