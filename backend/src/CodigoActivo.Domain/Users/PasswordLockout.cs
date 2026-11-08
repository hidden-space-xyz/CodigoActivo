namespace CodigoActivo.Domain.Users;

/// <summary>
/// The count of consecutive wrong passwords of an account and the temporary lock they lead to.
/// Locking forgets the count, so the count starts again once the lock expires; a new password lifts
/// the lock at once. The default value counts no failure and is not locked.
/// </summary>
/// <param name="FailedAttempts">Consecutive wrong passwords since the last accepted one or lock.</param>
/// <param name="LockedUntil">Until when every password, correct or not, is refused.</param>
public readonly record struct PasswordLockout(int FailedAttempts, DateTimeOffset? LockedUntil)
{
    /// <summary>
    /// Determines whether every password, correct or not, must be refused.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <returns><see langword="true"/> while the lock lasts.</returns>
    public bool IsLocked(DateTimeOffset now)
    {
        return LockedUntil > now;
    }

    /// <summary>
    /// Tells whether one more wrong password reaches the maximum and locks. While a lock lasts no
    /// wrong password locks again.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <param name="maxFailedAttempts">Wrong passwords that lock the account.</param>
    /// <returns><see langword="true"/> when the next wrong password locks.</returns>
    public bool LocksOnNextFailure(DateTimeOffset now, int maxFailedAttempts)
    {
        return !IsLocked(now) && FailedAttempts + 1 >= maxFailedAttempts;
    }

    /// <summary>
    /// Counts a wrong password, or locks for the lockout duration and forgets the count once the
    /// maximum is reached. While a lock lasts nothing is counted.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <param name="maxFailedAttempts">Wrong passwords that lock the account.</param>
    /// <param name="lockoutDuration">How long the lock lasts.</param>
    /// <returns>The lockout after the wrong password.</returns>
    public PasswordLockout AfterFailure(
        DateTimeOffset now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration
    )
    {
        if (IsLocked(now))
        {
            return this;
        }

        return LocksOnNextFailure(now, maxFailedAttempts)
            ? new PasswordLockout(0, now + lockoutDuration)
            : this with
            {
                FailedAttempts = FailedAttempts + 1,
            };
    }

    /// <summary>
    /// Forgets the wrong passwords counted so far without lifting a lock.
    /// </summary>
    /// <returns>The lockout without failures.</returns>
    public PasswordLockout WithoutFailures()
    {
        return this with { FailedAttempts = 0 };
    }
}
