using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Infrastructure.Communication;

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
        return kind is EmailKind.AccountVerification or EmailKind.PasswordReset;
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
        public Bucket Hourly { get; set; } = hourly;

        public Bucket Daily { get; set; } = daily;

        public bool Throttled { get; set; }
    }

    private readonly record struct Bucket(double Tokens, DateTimeOffset UpdatedAt)
    {
        public static Bucket Full(double capacity, DateTimeOffset now)
        {
            return new Bucket(capacity, now);
        }

        public Bucket Refill(DateTimeOffset now, double capacity, double perHour)
        {
            var elapsed = now - UpdatedAt;
            return elapsed <= TimeSpan.Zero ? this : new Bucket(Math.Min(capacity, Tokens + (elapsed.TotalHours * perHour)), now);
        }

        public Bucket Consume()
        {
            return this with { Tokens = Tokens - 1 };
        }

        public bool IsFull(double capacity)
        {
            return Tokens >= capacity;
        }
    }
}
