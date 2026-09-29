namespace CodigoActivo.Application.Accounts;

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
);

/// <summary>
/// Describes a session just opened for an account.
/// </summary>
/// <param name="SessionId">Identifier of the stored session.</param>
/// <param name="Identity">Account the session belongs to.</param>
public sealed record SessionTicket(Guid SessionId, SessionIdentity Identity);

/// <summary>
/// Describes the second-factor challenge an account has pending after its password step.
/// </summary>
/// <param name="ChallengeId">Identifier of the pending challenge.</param>
/// <param name="CredentialStamp">Stamp of the current password hash.</param>
public sealed record PendingChallenge(Guid ChallengeId, string CredentialStamp);
