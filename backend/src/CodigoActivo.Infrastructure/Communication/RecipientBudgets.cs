namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Represents the outcome of <see cref="RecipientBudgets.TrySpend"/>.
/// </summary>
/// <param name="Allowed">Whether the message may go to the address now.</param>
/// <param name="RetryAfter">
/// How long until the address has a message to spend again; zero when <paramref name="Allowed"/>.
/// </param>
/// <param name="Saturated">
/// Whether the address was let through untracked because the budgets already held
/// <see cref="EmailGuardOptions.MaxTrackedRecipients"/> addresses.
/// </param>
public readonly record struct RecipientSpend(bool Allowed, TimeSpan RetryAfter, bool Saturated);

/// <summary>
/// Keeps an hourly and a daily token bucket per address key, with the limits of
/// <see cref="EmailGuardOptions"/>, behind a lock of its own. Budgets that are full again are dropped
/// every <see cref="EmailGuardOptions.SweepInterval"/>, and at most
/// <see cref="EmailGuardOptions.MaxTrackedRecipients"/> are held: a new address past that is let
/// through untracked and reported.
/// </summary>
/// <param name="options">Per-recipient limits and the bounds of the tracking.</param>
/// <param name="startedAt">Time the budgets start from.</param>
public sealed class RecipientBudgets(EmailGuardOptions options, DateTimeOffset startedAt)
{
    private const double HoursPerDay = 24;

    private readonly Lock gate = new();
    private readonly Dictionary<string, RecipientState> budgets = new(StringComparer.Ordinal);

    private DateTimeOffset lastSweepAt = startedAt;
    private bool lastSweepFreedRoom = true;

    /// <summary>
    /// Gets how many address budgets are held.
    /// </summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return budgets.Count;
            }
        }
    }

    /// <summary>
    /// Spends one message from the hourly and the daily budget of an address when both allow it;
    /// a denial spends nothing.
    /// </summary>
    /// <param name="key">Key of the address, already normalized.</param>
    /// <param name="now">Current time.</param>
    /// <returns>Whether the message may go now and, if not, when it may.</returns>
    public RecipientSpend TrySpend(string key, DateTimeOffset now)
    {
        lock (gate)
        {
            SweepIfDue(now);

            if (!budgets.TryGetValue(key, out var state))
            {
                if (budgets.Count >= options.MaxTrackedRecipients)
                {
                    return new RecipientSpend(true, TimeSpan.Zero, Saturated: true);
                }

                state = new RecipientState(
                    TokenBucket.Full(options.RecipientBurst, now),
                    TokenBucket.Full(options.RecipientPerDay, now)
                );
                budgets[key] = state;
            }

            state.Hourly = state.Hourly.Refill(
                now,
                options.RecipientBurst,
                options.RecipientPerHour
            );
            state.Daily = state.Daily.Refill(now, options.RecipientPerDay, DailyRefill);

            if (state.Hourly.Tokens < 1 || state.Daily.Tokens < 1)
            {
                var wait = Max(
                    state.Hourly.TimeUntilOne(options.RecipientPerHour),
                    state.Daily.TimeUntilOne(DailyRefill)
                );
                return new RecipientSpend(false, wait, Saturated: false);
            }

            state.Hourly = state.Hourly.Consume();
            state.Daily = state.Daily.Consume();
            return new RecipientSpend(true, TimeSpan.Zero, Saturated: false);
        }
    }

    private double DailyRefill => options.RecipientPerDay / HoursPerDay;

    private void SweepIfDue(DateTimeOffset now)
    {
        var scheduled = now - lastSweepAt >= options.SweepInterval;
        var saturated = lastSweepFreedRoom && budgets.Count >= options.MaxTrackedRecipients;
        if (!scheduled && !saturated)
        {
            return;
        }

        lastSweepAt = now;

        List<string>? stale = null;
        foreach (var (key, state) in budgets)
        {
            var hourly = state.Hourly.Refill(now, options.RecipientBurst, options.RecipientPerHour);
            var daily = state.Daily.Refill(now, options.RecipientPerDay, DailyRefill);

            if (hourly.IsFull(options.RecipientBurst) && daily.IsFull(options.RecipientPerDay))
            {
                (stale ??= []).Add(key);
            }
        }

        lastSweepFreedRoom = stale is not null;
        foreach (var key in stale ?? [])
        {
            budgets.Remove(key);
        }
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second)
    {
        return first >= second ? first : second;
    }

    private sealed class RecipientState(TokenBucket hourly, TokenBucket daily)
    {
        /// <summary>
        /// Gets or sets the budget that holds <see cref="EmailGuardOptions.RecipientBurst"/> and gets
        /// <see cref="EmailGuardOptions.RecipientPerHour"/> back per hour.
        /// </summary>
        public TokenBucket Hourly { get; set; } = hourly;

        /// <summary>
        /// Gets or sets the budget that holds <see cref="EmailGuardOptions.RecipientPerDay"/> and gets
        /// it back over a day.
        /// </summary>
        public TokenBucket Daily { get; set; } = daily;
    }
}

/// <summary>
/// Represents a token bucket: the permits it holds and when they were last counted.
/// </summary>
/// <param name="Tokens">Permits held.</param>
/// <param name="UpdatedAt">Time the permits were last counted.</param>
public readonly record struct TokenBucket(double Tokens, DateTimeOffset UpdatedAt)
{
    /// <summary>
    /// Creates a bucket that holds its whole capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of permits held by the bucket.</param>
    /// <param name="now">Current timestamp, from which the bucket refills.</param>
    /// <returns>The full bucket.</returns>
    public static TokenBucket Full(double capacity, DateTimeOffset now)
    {
        return new TokenBucket(capacity, now);
    }

    /// <summary>
    /// Replenishes the bucket according to elapsed time, up to its capacity. A clock that has not
    /// moved forward leaves the bucket as it is.
    /// </summary>
    /// <param name="now">Current timestamp used to calculate replenishment.</param>
    /// <param name="capacity">Maximum number of permits held by the bucket.</param>
    /// <param name="perHour">Number of permits replenished per hour.</param>
    /// <returns>The refilled bucket.</returns>
    public TokenBucket Refill(DateTimeOffset now, double capacity, double perHour)
    {
        var elapsed = now - UpdatedAt;
        return elapsed <= TimeSpan.Zero
            ? this
            : new TokenBucket(Math.Min(capacity, Tokens + (elapsed.TotalHours * perHour)), now);
    }

    /// <summary>
    /// Takes one permit from the bucket; the caller checks first that one is available.
    /// </summary>
    /// <returns>The bucket with one permit fewer.</returns>
    public TokenBucket Consume()
    {
        return this with { Tokens = Tokens - 1 };
    }

    /// <summary>
    /// Determines whether the bucket is at capacity.
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

    /// <summary>
    /// Gets how long the bucket needs to hold one whole permit again.
    /// </summary>
    /// <param name="perHour">Number of permits replenished per hour.</param>
    /// <returns>
    /// Zero when it already holds one; otherwise the time until it does, or a day when it never
    /// refills.
    /// </returns>
    public TimeSpan TimeUntilOne(double perHour)
    {
        if (Tokens >= 1)
        {
            return TimeSpan.Zero;
        }

        return perHour > 0 ? TimeSpan.FromHours((1 - Tokens) / perHour) : TimeSpan.FromDays(1);
    }
}
