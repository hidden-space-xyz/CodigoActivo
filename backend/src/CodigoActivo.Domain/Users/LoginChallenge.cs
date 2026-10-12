namespace CodigoActivo.Domain.Users;

/// <summary>
/// The second-factor challenge an account has open after an accepted password. The challenge
/// cookie carries its identifier, so a copied cookie stops working as soon as the identifier
/// changes or is cleared. The default value is no open challenge.
/// </summary>
/// <param name="Id">Identifier of the open challenge.</param>
/// <param name="StartedAt">When the challenge was opened, which bounds how long it is accepted.</param>
public readonly record struct LoginChallenge(LoginChallengeId? Id, DateTimeOffset? StartedAt)
{
    /// <summary>
    /// Opens a challenge now under an identifier.
    /// </summary>
    /// <param name="challengeId">Identifier the challenge cookie will carry.</param>
    /// <param name="now">Current timestamp.</param>
    /// <returns>The open challenge.</returns>
    public static LoginChallenge Start(LoginChallengeId challengeId, DateTimeOffset now)
    {
        return new LoginChallenge(challengeId, now);
    }

    /// <summary>
    /// Tells whether a challenge is this one.
    /// </summary>
    /// <param name="challengeId">Identifier of the challenge.</param>
    /// <returns><see langword="true"/> when it is the open challenge.</returns>
    public bool Matches(LoginChallengeId challengeId)
    {
        return Id == challengeId;
    }

    /// <summary>
    /// Tells whether a challenge is this one and is still young enough to be answered.
    /// </summary>
    /// <param name="challengeId">Identifier of the challenge.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="lifetime">How long a challenge can be answered after it opens.</param>
    /// <returns><see langword="true"/> when the challenge can still be answered.</returns>
    public bool IsOpen(LoginChallengeId challengeId, DateTimeOffset now, TimeSpan lifetime)
    {
        return Matches(challengeId) && now < StartedAt + lifetime;
    }
}
