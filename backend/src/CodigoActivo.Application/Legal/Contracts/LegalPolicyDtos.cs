using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.Application.Legal.Contracts;

/// <summary>
/// Contains one version of a legal policy, the one in force or one a later version replaced.
/// </summary>
/// <param name="Kind">Which policy of the site it is.</param>
/// <param name="Version">Number of the version; it grows with every change.</param>
/// <param name="Content">Rich-text content of the version.</param>
/// <param name="PublishedAt">When the version was published.</param>
public record LegalPolicyResponse(
    LegalPolicyKind Kind,
    int Version,
    string Content,
    DateTimeOffset PublishedAt
)
{
    /// <summary>
    /// Initializes an empty legal policy response for serialization.
    /// </summary>
    public LegalPolicyResponse()
        : this(LegalPolicyKind.Privacy, 0, string.Empty, default) { }
}

/// <summary>
/// Contains when a version of a legal policy was published, without its content.
/// </summary>
/// <param name="Version">Number of the version.</param>
/// <param name="PublishedAt">When the version was published.</param>
public record LegalPolicyVersionResponse(int Version, DateTimeOffset PublishedAt)
{
    /// <summary>
    /// Initializes an empty legal policy version response for serialization.
    /// </summary>
    public LegalPolicyVersionResponse()
        : this(0, default) { }
}
