namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Which policy of the site a legal policy is, as clients send and receive it.
/// </summary>
public enum LegalPolicyKind
{
    /// <summary>
    /// The privacy policy.
    /// </summary>
    Privacy = 1,

    /// <summary>
    /// The cookie policy.
    /// </summary>
    Cookies = 2,
}
