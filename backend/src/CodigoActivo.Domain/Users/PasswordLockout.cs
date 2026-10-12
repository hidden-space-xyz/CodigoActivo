namespace CodigoActivo.Domain.Users;

/// <summary>
/// The count of consecutive wrong passwords of an account and the lock they lead to. The lock does
/// not expire: only a completed password reset lifts it. The default value counts no failure and is
/// not locked.
/// </summary>
/// <param name="FailedAttempts">Consecutive wrong passwords since the last accepted one.</param>
/// <param name="LockedAt">When the account was locked after too many wrong passwords.</param>
public readonly record struct PasswordLockout(int FailedAttempts, DateTimeOffset? LockedAt)
{
    /// <summary>
    /// Gets a value indicating whether every password, correct or not, must be refused.
    /// </summary>
    public bool IsLocked => LockedAt is not null;

    /// <summary>
    /// Tells whether one more wrong password reaches the maximum and locks. A lock that already
    /// holds is never applied again.
    /// </summary>
    /// <param name="maxFailedAttempts">Wrong passwords that lock the account.</param>
    /// <returns><see langword="true"/> when the next wrong password locks.</returns>
    public bool LocksOnNextFailure(int maxFailedAttempts)
    {
        return !IsLocked && FailedAttempts + 1 >= maxFailedAttempts;
    }

    /// <summary>
    /// Counts a wrong password and locks once the count reaches the maximum. A lock counts nothing
    /// more.
    /// </summary>
    /// <param name="maxFailedAttempts">Wrong passwords that lock the account.</param>
    /// <param name="now">Current time, stored as the locking time.</param>
    /// <returns>The lockout after the wrong password.</returns>
    public PasswordLockout AfterFailure(int maxFailedAttempts, DateTimeOffset now)
    {
        return IsLocked
            ? this
            : new PasswordLockout(
                FailedAttempts + 1,
                LocksOnNextFailure(maxFailedAttempts) ? now : null
            );
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
