namespace CodigoActivo.Domain.Events;

/// <summary>
/// When people may sign up for an event: an optional early period reserved to entitled people,
/// then the open period, which ends after it starts. Instants are in UTC.
/// </summary>
public sealed record SignupWindow
{
    private SignupWindow(
        DateTimeOffset? earlyStartsAt,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt
    )
    {
        EarlyStartsAt = earlyStartsAt;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    /// <summary>
    /// Gets when the early signup opens, or <see langword="null"/> when there is none.
    /// </summary>
    public DateTimeOffset? EarlyStartsAt { get; }

    /// <summary>
    /// Gets when the signup opens to everyone.
    /// </summary>
    public DateTimeOffset StartsAt { get; }

    /// <summary>
    /// Gets when the signup closes.
    /// </summary>
    public DateTimeOffset EndsAt { get; }

    /// <summary>
    /// Creates a window whose open period ends after it starts and whose early period, if any,
    /// starts before the open one.
    /// </summary>
    /// <param name="earlyStartsAt">When the early signup opens, if there is one.</param>
    /// <param name="startsAt">When the signup opens to everyone.</param>
    /// <param name="endsAt">When the signup closes.</param>
    /// <returns>The window in UTC, or <see langword="null"/> when the instants are out of order.</returns>
    public static SignupWindow? TryCreate(
        DateTimeOffset? earlyStartsAt,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt
    )
    {
        return endsAt <= startsAt || earlyStartsAt >= startsAt
            ? null
            : new SignupWindow(
                earlyStartsAt?.ToUniversalTime(),
                startsAt.ToUniversalTime(),
                endsAt.ToUniversalTime()
            );
    }

    /// <summary>
    /// Restores a window that was checked before it was stored.
    /// </summary>
    /// <param name="earlyStartsAt">Stored opening of the early signup.</param>
    /// <param name="startsAt">Stored opening of the signup.</param>
    /// <param name="endsAt">Stored closing of the signup.</param>
    /// <returns>The window.</returns>
    public static SignupWindow FromStored(
        DateTimeOffset? earlyStartsAt,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt
    )
    {
        return new SignupWindow(earlyStartsAt, startsAt, endsAt);
    }

    /// <summary>
    /// Gets the signup phase at an instant.
    /// </summary>
    /// <param name="now">Instant to evaluate.</param>
    /// <returns>The phase of the window at <paramref name="now"/>.</returns>
    public SignupPhase PhaseAt(DateTimeOffset now)
    {
        return EventTimeline.SignupPhaseAt(EarlyStartsAt, StartsAt, EndsAt, now);
    }
}
