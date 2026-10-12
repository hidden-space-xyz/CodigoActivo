namespace CodigoActivo.Domain.Users;

/// <summary>
/// A move of an account to a new email that waits until the code emailed to the new address is
/// confirmed. The default value holds no change.
/// </summary>
/// <param name="NewEmail">Email the holder asked to move the account to.</param>
/// <param name="CodeHash">Hash of the emailed code that confirms the change.</param>
/// <param name="ExpiresAt">When the code that confirms the change expires.</param>
public readonly record struct EmailChange(
    EmailAddress? NewEmail,
    string? CodeHash,
    DateTimeOffset? ExpiresAt
)
{
    /// <summary>
    /// Creates a change to a new email whose code was emailed now.
    /// </summary>
    /// <param name="newEmail">Email the account moves to once the code is confirmed.</param>
    /// <param name="codeHash">Hash of the code emailed to the new address.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="lifetime">How long the emailed code stays valid.</param>
    /// <returns>The change.</returns>
    public static EmailChange Request(
        EmailAddress newEmail,
        string codeHash,
        DateTimeOffset now,
        TimeSpan lifetime
    )
    {
        return new EmailChange(newEmail, codeHash, now + lifetime);
    }

    /// <summary>
    /// Gets the hash of the code that confirms the change while the code can still be used.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The hash, or <see langword="null"/> when no change waits or its code expired.</returns>
    public string? UsableCodeHash(DateTimeOffset now)
    {
        return ExpiresAt >= now ? CodeHash : null;
    }
}
