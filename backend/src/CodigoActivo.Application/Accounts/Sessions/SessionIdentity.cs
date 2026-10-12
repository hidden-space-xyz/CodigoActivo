namespace CodigoActivo.Application.Accounts.Sessions;

/// <summary>
/// Identifies the account behind a session: what the session cookie claims carry.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
/// <param name="FirstName">Given name of the account holder.</param>
/// <param name="LastName">Family name of the account holder.</param>
/// <param name="Email">Email address of the account, if any.</param>
/// <param name="IsAdmin">Whether the account is an administrator.</param>
/// <param name="CredentialStamp">Stamp of the current password hash.</param>
public sealed record SessionIdentity(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    bool IsAdmin,
    string CredentialStamp
)
{
    /// <summary>
    /// Tells whether a cookie still belongs to this identity: its stamp must be the one of the
    /// current password, so changing or resetting the password ends every session.
    /// </summary>
    /// <param name="presentedStamp">Credential stamp the cookie carries.</param>
    /// <returns><see langword="true"/> when the cookie was issued for the current password.</returns>
    public bool Accepts(string presentedStamp)
    {
        return CredentialStamps.Matches(presentedStamp, CredentialStamp);
    }
}
