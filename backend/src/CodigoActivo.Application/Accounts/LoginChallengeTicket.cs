namespace CodigoActivo.Application.Accounts;

/// <summary>
/// What the challenge cookie of an open second-factor challenge carries besides its identifiers.
/// </summary>
/// <param name="CredentialStamp">Stamp of the current password hash.</param>
public sealed record LoginChallengeTicket(string CredentialStamp)
{
    /// <summary>
    /// Tells whether a challenge cookie still belongs to this challenge: its stamp must be the one
    /// of the current password, so a password change refuses a challenge opened before it.
    /// </summary>
    /// <param name="presentedStamp">Credential stamp the cookie carries.</param>
    /// <returns><see langword="true"/> when the cookie was issued for the current password.</returns>
    public bool Accepts(string presentedStamp)
    {
        return CredentialStamps.Matches(presentedStamp, CredentialStamp);
    }
}
