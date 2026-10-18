using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Issues and reads the tokens of the unsubscribe links in news email. A token names its
/// account without a session and cannot be forged or altered, so following the link is enough to
/// withdraw the news consent.
/// </summary>
public interface IUnsubscribeTokens
{
    /// <summary>
    /// Issues the token of the unsubscribe link of an account.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <returns>The token, safe to put in a URL.</returns>
    public string Issue(UserId userId);

    /// <summary>
    /// Reads a token issued by <see cref="Issue"/>.
    /// </summary>
    /// <param name="token">Token taken from the link.</param>
    /// <param name="userId">
    /// Identifier of the account the token was issued for; the default value when it was not.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the token was issued here and was not altered.
    /// </returns>
    public bool TryRead(string token, out UserId userId);
}
