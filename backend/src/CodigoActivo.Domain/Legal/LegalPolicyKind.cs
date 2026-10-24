namespace CodigoActivo.Domain.Legal;

/// <summary>
/// Names a policy the site publishes about the people who use it; there is at most one policy of
/// each kind.
/// </summary>
public enum LegalPolicyKind
{
    /// <summary>
    /// The privacy policy: which personal data is processed, why, on which legal basis, for how
    /// long and how to exercise the rights over it.
    /// </summary>
    Privacy = 1,

    /// <summary>
    /// The cookie policy: what the site stores in the browser, what for and for how long.
    /// </summary>
    Cookies = 2,
}
