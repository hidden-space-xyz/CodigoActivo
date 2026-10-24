using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Legal;

/// <summary>
/// A version of a legal policy that a later revision replaced, with its content and when it was
/// published. It is kept so the text in force on any date can always be shown.
/// </summary>
public class LegalPolicyVersion
{
    private LegalPolicyVersion() { }

    internal LegalPolicyVersion(LegalPolicy policy)
    {
        LegalPolicyId = policy.Id;
        Number = policy.Version;
        Content = policy.Content;
        PublishedAt = policy.PublishedAt;
    }

    /// <summary>
    /// Gets the identifier of the legal policy.
    /// </summary>
    public LegalPolicyId LegalPolicyId { get; private set; }

    /// <summary>
    /// Gets the number of the version, from <see cref="LegalPolicy.FirstVersion"/> up.
    /// </summary>
    public int Number { get; private set; }

    /// <summary>
    /// Gets the rich-text content of this version.
    /// </summary>
    public RichText Content { get; private set; } = RichText.Empty;

    /// <summary>
    /// Gets when this version was published; it stayed in force until the next one was.
    /// </summary>
    public DateTimeOffset PublishedAt { get; private set; }
}
