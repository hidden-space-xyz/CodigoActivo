namespace CodigoActivo.Domain.Users;

/// <summary>
/// The authenticator application of an account: the protected shared secret of the active one with
/// the last time step it accepted, and an enrollment that waits for its first code. The default
/// value holds neither.
/// </summary>
/// <param name="Key">Protected shared secret of the active authenticator application.</param>
/// <param name="LastUsedStep">Last time step accepted, so a code cannot be replayed.</param>
/// <param name="PendingKey">Protected shared secret of an authenticator being enrolled.</param>
/// <param name="PendingExpiresAt">When the pending enrollment stops being confirmable.</param>
public readonly record struct Authenticator(
    string? Key,
    long? LastUsedStep,
    string? PendingKey,
    DateTimeOffset? PendingExpiresAt
)
{
    /// <summary>
    /// Starts an enrollment that can be confirmed for its lifetime, replacing any pending one and
    /// keeping the active authenticator.
    /// </summary>
    /// <param name="protectedKey">Protected shared secret being enrolled.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="lifetime">How long the enrollment can be confirmed.</param>
    /// <returns>The authenticator with the pending enrollment.</returns>
    public Authenticator BeginEnrollment(string protectedKey, DateTimeOffset now, TimeSpan lifetime)
    {
        return this with { PendingKey = protectedKey, PendingExpiresAt = now + lifetime };
    }

    /// <summary>
    /// Determines whether an enrollment is waiting for confirmation.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <returns><see langword="true"/> when a pending key exists and has not expired.</returns>
    public bool HasPendingEnrollment(DateTimeOffset now)
    {
        return PendingKey is not null && PendingExpiresAt > now;
    }

    /// <summary>
    /// Makes the pending enrollment the active authenticator.
    /// </summary>
    /// <param name="usedStep">Time step of the code that confirmed the enrollment.</param>
    /// <returns>The active authenticator, with no enrollment pending.</returns>
    public Authenticator Activate(long usedStep)
    {
        return new Authenticator(PendingKey, usedStep, PendingKey: null, PendingExpiresAt: null);
    }

    /// <summary>
    /// Tells whether a code of the active authenticator may be accepted. A code of the time step
    /// already accepted, or of an earlier one, is refused, so a code cannot be used twice.
    /// </summary>
    /// <param name="step">Time step of the code.</param>
    /// <returns><see langword="true"/> when the step is newer than the last one accepted.</returns>
    public bool Accepts(long step)
    {
        return LastUsedStep is not { } lastUsed || step > lastUsed;
    }

    /// <summary>
    /// Remembers the time step of an accepted code.
    /// </summary>
    /// <param name="step">Time step of the accepted code.</param>
    /// <returns>The authenticator that refuses that step from now on.</returns>
    public Authenticator WithUsedStep(long step)
    {
        return this with { LastUsedStep = step };
    }
}
