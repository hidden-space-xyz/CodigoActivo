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
/// Enforces the limits of <see cref="EmailGuardOptions"/> with token buckets kept in memory behind a
/// lock: a global one, and an hourly and a daily one per normalized address. The global budget is
/// tiered: ordinary mail leaves the credential reserve untouched, and verification and password-reset
/// mail, which anyone can trigger, leave the part of it kept for second-factor codes, which only a
/// correct password triggers. Second-factor codes also have budgets of their own per destination, so
/// flooding an address with other mail never stops its owner from receiving a login code.
/// </summary>
/// <param name="options">Limits applied, and the interval that spaces repeated alerts.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class EmailSendLimiter(EmailGuardOptions options, IClock clock)
{
    private const double HoursPerDay = 24;

    private readonly Lock gate = new();
    private readonly Dictionary<RecipientKey, RecipientState> recipients = [];

    private readonly LogThrottle globalExhaustedAlerts = new(options.AlertInterval);
    private readonly LogThrottle saturationAlerts = new(options.AlertInterval);

    private Bucket global = Bucket.Full(options.GlobalBurst, clock.UtcNow);
    private DateTimeOffset lastSweepAt = clock.UtcNow;
    private bool lastSweepFreedRoom = true;

    /// <summary>
    /// Gets how many address budgets the limiter holds; an address that received second-factor codes
    /// and other mail counts twice.
    /// </summary>
    public int TrackedRecipients
    {
        get
        {
            lock (gate)
            {
                return recipients.Count;
            }
        }
    }

    /// <summary>
    /// Checks the global budget, less the reserve the kind may not spend, and then the hourly and
    /// daily budgets of the address, and spends one message from each when all of them allow it. A
    /// denial spends nothing. A new address while <see cref="EmailGuardOptions.MaxTrackedRecipients"/>
    /// budgets are held spends the global budget only.
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
        var key = new RecipientKey(kind is EmailKind.TwoFactorCode, NormalizeKey(address));
        var reserve = ReserveFor(kind);

        lock (gate)
        {
            global = global.Refill(now, options.GlobalBurst, options.GlobalPerHour);

            if (global.Tokens - reserve < 1)
            {
                return Denied(EmailLimitScope.Global, GlobalExhaustedAlert(now));
            }

            SweepIfDue(now);

            if (!recipients.TryGetValue(key, out var state))
            {
                if (recipients.Count >= options.MaxTrackedRecipients)
                {
                    global = global.Consume();
                    return new EmailSendDecision(EmailLimitScope.None, SaturationAlert(now));
                }

                state = new RecipientState(
                    Bucket.Full(options.RecipientBurst, now),
                    Bucket.Full(options.RecipientPerDay, now)
                );
                recipients[key] = state;
            }

            var hourly = state.Hourly.Refill(now, options.RecipientBurst, options.RecipientPerHour);
            var daily = state.Daily.Refill(
                now,
                options.RecipientPerDay,
                options.RecipientPerDay / HoursPerDay
            );

            state.Hourly = hourly;
            state.Daily = daily;

            if (hourly.Tokens < 1 || daily.Tokens < 1)
            {
                return Denied(EmailLimitScope.Recipient, EmailGuardAlert.None);
            }

            state.Hourly = hourly.Consume();
            state.Daily = daily.Consume();
            global = global.Consume();

            return new EmailSendDecision(EmailLimitScope.None, EmailGuardAlert.None);
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

    private int ReserveFor(EmailKind kind)
    {
        return kind switch
        {
            EmailKind.TwoFactorCode => 0,
            EmailKind.AccountVerification or EmailKind.AccessRecovery =>
                options.EffectiveTwoFactorReserve,
            _ => options.EffectiveCredentialReserve,
        };
    }

    private static EmailSendDecision Denied(EmailLimitScope scope, EmailGuardAlert alert)
    {
        return new EmailSendDecision(scope, alert);
    }

    private EmailGuardAlert GlobalExhaustedAlert(DateTimeOffset now)
    {
        return globalExhaustedAlerts.TryEnter(now)
            ? EmailGuardAlert.GlobalBudgetExhausted
            : EmailGuardAlert.None;
    }

    private EmailGuardAlert SaturationAlert(DateTimeOffset now)
    {
        return saturationAlerts.TryEnter(now)
            ? EmailGuardAlert.TrackingSaturated
            : EmailGuardAlert.None;
    }

    private void SweepIfDue(DateTimeOffset now)
    {
        var scheduled = now - lastSweepAt >= options.SweepInterval;
        var saturated = lastSweepFreedRoom && recipients.Count >= options.MaxTrackedRecipients;
        if (!scheduled && !saturated)
        {
            return;
        }

        lastSweepAt = now;

        List<RecipientKey>? stale = null;
        foreach (var (key, state) in recipients)
        {
            var hourly = state.Hourly.Refill(now, options.RecipientBurst, options.RecipientPerHour);
            var daily = state.Daily.Refill(
                now,
                options.RecipientPerDay,
                options.RecipientPerDay / HoursPerDay
            );

            if (hourly.IsFull(options.RecipientBurst) && daily.IsFull(options.RecipientPerDay))
            {
                (stale ??= []).Add(key);
            }
        }

        lastSweepFreedRoom = stale is not null;
        if (stale is null)
        {
            return;
        }

        foreach (var key in stale)
        {
            recipients.Remove(key);
        }
    }

    private readonly record struct RecipientKey(bool IsTwoFactorCode, string Address);

    private sealed class RecipientState(Bucket hourly, Bucket daily)
    {
        /// <summary>
        /// Gets or sets the budget that holds <see cref="EmailGuardOptions.RecipientBurst"/> and gets
        /// <see cref="EmailGuardOptions.RecipientPerHour"/> back per hour.
        /// </summary>
        public Bucket Hourly { get; set; } = hourly;

        /// <summary>
        /// Gets or sets the budget that holds <see cref="EmailGuardOptions.RecipientPerDay"/> and gets
        /// it back over a day.
        /// </summary>
        public Bucket Daily { get; set; } = daily;
    }

    private readonly record struct Bucket(double Tokens, DateTimeOffset UpdatedAt)
    {
        /// <summary>
        /// Creates a bucket that holds its whole capacity.
        /// </summary>
        /// <param name="capacity">Maximum number of permits held by the bucket.</param>
        /// <param name="now">Current timestamp, from which the bucket refills.</param>
        /// <returns>The full bucket.</returns>
        public static Bucket Full(double capacity, DateTimeOffset now)
        {
            return new Bucket(capacity, now);
        }

        /// <summary>
        /// Replenishes the rate-limit bucket according to elapsed time, up to its capacity. A clock that
        /// has not moved forward leaves the bucket as it is.
        /// </summary>
        /// <param name="now">Current timestamp used to calculate replenishment.</param>
        /// <param name="capacity">Maximum number of permits held by the bucket.</param>
        /// <param name="perHour">Number of permits replenished per hour.</param>
        /// <returns>The refilled bucket.</returns>
        public Bucket Refill(DateTimeOffset now, double capacity, double perHour)
        {
            var elapsed = now - UpdatedAt;
            return elapsed <= TimeSpan.Zero
                ? this
                : new Bucket(Math.Min(capacity, Tokens + (elapsed.TotalHours * perHour)), now);
        }

        /// <summary>
        /// Takes one permit from the rate-limit bucket; the caller checks first that one is available.
        /// </summary>
        /// <returns>The bucket with one permit fewer.</returns>
        public Bucket Consume()
        {
            return this with { Tokens = Tokens - 1 };
        }

        /// <summary>
        /// Determines whether the rate-limit bucket is at capacity.
        /// </summary>
        /// <param name="capacity">Maximum number of permits held by the bucket.</param>
        /// <returns>
        /// <see langword="true"/> when the bucket holds at least <paramref name="capacity"/> permits;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public bool IsFull(double capacity)
        {
            return Tokens >= capacity;
        }
    }
}
