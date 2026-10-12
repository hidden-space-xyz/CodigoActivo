namespace CodigoActivo.Domain.Users;

/// <summary>
/// The count of consecutive wrong second-factor codes of an account and the temporary lock they
/// lead to. Locking forgets the count. The default value counts no failure and is not locked.
/// </summary>
/// <param name="FailedAttempts">Consecutive wrong codes since the last success or lock.</param>
/// <param name="LockedUntil">Until when second-factor codes are rejected.</param>
public readonly record struct TwoFactorLockout(int FailedAttempts, DateTimeOffset? LockedUntil)
{
    /// <summary>
    /// Determines whether second-factor codes must be rejected regardless of their value.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <returns><see langword="true"/> while the lock lasts.</returns>
    public bool IsLocked(DateTimeOffset now)
    {
        return LockedUntil > now;
    }

    /// <summary>
    /// Tells whether one more wrong code reaches the limit and locks.
    /// </summary>
    /// <param name="maxFailedAttempts">Failures allowed before locking.</param>
    /// <returns><see langword="true"/> when the next wrong code locks.</returns>
    public bool LocksOnNextFailure(int maxFailedAttempts)
    {
        return FailedAttempts + 1 >= maxFailedAttempts;
    }

    /// <summary>
    /// Counts a wrong code, or locks for the lockout duration and forgets the count once the limit
    /// is reached.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <param name="maxFailedAttempts">Failures allowed before locking.</param>
    /// <param name="lockoutDuration">How long the lock lasts.</param>
    /// <returns>The lockout after the wrong code.</returns>
    public TwoFactorLockout AfterFailure(
        DateTimeOffset now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration
    )
    {
        return LocksOnNextFailure(maxFailedAttempts)
            ? new TwoFactorLockout(0, now + lockoutDuration)
            : this with
            {
                FailedAttempts = FailedAttempts + 1,
            };
    }

    /// <summary>
    /// Forgets the wrong codes counted so far without lifting a lock.
    /// </summary>
    /// <returns>The lockout without failures.</returns>
    public TwoFactorLockout WithoutFailures()
    {
        return this with { FailedAttempts = 0 };
    }
}
