using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Represents an email send decision value used by the application.
/// </summary>
/// <param name="Scope">The scope value.</param>
/// <param name="Alert">The alert value.</param>
/// <param name="GlobalRemaining">The global remaining value.</param>
public readonly record struct EmailSendDecision(
    EmailLimitScope Scope,
    EmailGuardAlert Alert,
    int GlobalRemaining
);

/// <summary>
/// Enforces the configured limits for email send.
/// </summary>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class EmailSendLimiter(EmailGuardOptions options, IClock clock)
{
    private const double GlobalLowWatermark = 0.2;
    private const double HoursPerDay = 24;

    private readonly Lock gate = new();
    private readonly Dictionary<string, RecipientState> recipients = new(StringComparer.Ordinal);

    private Bucket global = Bucket.Full(options.GlobalBurst, clock.UtcNow);
    private DateTimeOffset lastSweepAt = clock.UtcNow;
    private bool lastSweepFreedRoom = true;
    private DateTimeOffset? lastGlobalLowAlertAt;
    private DateTimeOffset? lastGlobalExhaustedAlertAt;
    private DateTimeOffset? lastSaturationAlertAt;

    /// <summary>
    /// Gets the tracked recipients value.
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
    /// Attempts to consume capacity from the applicable email rate-limit buckets.
    /// </summary>
    /// <param name="kind">Email category whose limits are applied.</param>
    /// <param name="address">Recipient address used as a rate-limit key.</param>
    /// <returns>The resulting email send decision value.</returns>
    public EmailSendDecision TryConsume(EmailKind kind, string address)
    {
        var now = clock.UtcNow;
        var key = NormalizeKey(address);
        var reserve = IsCredential(kind) ? 0 : options.EffectiveCredentialReserve;

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
                    return new EmailSendDecision(
                        EmailLimitScope.None,
                        SaturationAlert(now),
                        Remaining()
                    );
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
                var alert = state.Throttled
                    ? EmailGuardAlert.None
                    : EmailGuardAlert.RecipientThrottled;
                state.Throttled = true;
                return Denied(EmailLimitScope.Recipient, alert);
            }

            state.Hourly = hourly.Consume();
            state.Daily = daily.Consume();
            state.Throttled = false;
            global = global.Consume();

            return new EmailSendDecision(
                EmailLimitScope.None,
                GlobalLowAlert(now, reserve),
                Remaining()
            );
        }
    }

    /// <summary>
    /// Normalizes the supplied key value.
    /// </summary>
    /// <param name="address">Recipient address used as a rate-limit key.</param>
    /// <returns>The generated text.</returns>
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

    private static bool IsCredential(EmailKind kind)
    {
        return kind
            is EmailKind.AccountVerification
                or EmailKind.PasswordReset
                or EmailKind.TwoFactorCode;
    }

    private EmailSendDecision Denied(EmailLimitScope scope, EmailGuardAlert alert)
    {
        return new EmailSendDecision(scope, alert, Remaining());
    }

    private int Remaining()
    {
        return int.CreateTruncating(global.Tokens);
    }

    private EmailGuardAlert GlobalExhaustedAlert(DateTimeOffset now)
    {
        return ShouldAlert(ref lastGlobalExhaustedAlertAt, now)
            ? EmailGuardAlert.GlobalBudgetExhausted
            : EmailGuardAlert.None;
    }

    private EmailGuardAlert SaturationAlert(DateTimeOffset now)
    {
        return ShouldAlert(ref lastSaturationAlertAt, now)
            ? EmailGuardAlert.TrackingSaturated
            : EmailGuardAlert.None;
    }

    private EmailGuardAlert GlobalLowAlert(DateTimeOffset now, int reserve)
    {
        var available = global.Tokens - reserve;
        return available < options.GlobalBurst * GlobalLowWatermark
            && ShouldAlert(ref lastGlobalLowAlertAt, now)
            ? EmailGuardAlert.GlobalBudgetLow
            : EmailGuardAlert.None;
    }

    private bool ShouldAlert(ref DateTimeOffset? lastAt, DateTimeOffset now)
    {
        if (lastAt is not null && now - lastAt.Value < options.AlertInterval)
        {
            return false;
        }

        lastAt = now;
        return true;
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

        List<string>? stale = null;
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

    private sealed class RecipientState(Bucket hourly, Bucket daily)
    {
        /// <summary>
        /// Gets or sets the hourly value.
        /// </summary>
        public Bucket Hourly { get; set; } = hourly;

        /// <summary>
        /// Gets or sets the daily value.
        /// </summary>
        public Bucket Daily { get; set; } = daily;

        /// <summary>
        /// Gets or sets the throttled value.
        /// </summary>
        public bool Throttled { get; set; }
    }

    private readonly record struct Bucket(double Tokens, DateTimeOffset UpdatedAt)
    {
        /// <summary>
        /// Creates a rate-limit decision indicating that no capacity remains.
        /// </summary>
        /// <param name="capacity">Maximum number of permits held by the bucket.</param>
        /// <param name="now">Current timestamp used to calculate replenishment.</param>
        /// <returns>The resulting bucket value.</returns>
        public static Bucket Full(double capacity, DateTimeOffset now)
        {
            return new Bucket(capacity, now);
        }

        /// <summary>
        /// Replenishes the rate-limit bucket according to elapsed time.
        /// </summary>
        /// <param name="now">Current timestamp used to calculate replenishment.</param>
        /// <param name="capacity">Maximum number of permits held by the bucket.</param>
        /// <param name="perHour">Number of permits replenished per hour.</param>
        /// <returns>The resulting bucket value.</returns>
        public Bucket Refill(DateTimeOffset now, double capacity, double perHour)
        {
            var elapsed = now - UpdatedAt;
            return elapsed <= TimeSpan.Zero ? this : new Bucket(Math.Min(capacity, Tokens + (elapsed.TotalHours * perHour)), now);
        }

        /// <summary>
        /// Consumes one available permit from the rate-limit bucket.
        /// </summary>
        /// <returns>The resulting bucket value.</returns>
        public Bucket Consume()
        {
            return this with { Tokens = Tokens - 1 };
        }

        /// <summary>
        /// Determines whether the rate-limit bucket is at capacity.
        /// </summary>
        /// <param name="capacity">Maximum number of permits held by the bucket.</param>
        /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
        public bool IsFull(double capacity)
        {
            return Tokens >= capacity;
        }
    }
}
