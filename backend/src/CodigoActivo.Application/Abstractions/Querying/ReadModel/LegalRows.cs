namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Stored legal policy, with the version in force, as the queries read it.
/// </summary>
public sealed class LegalPolicyRow
{
    /// <summary>Gets the identifier of the policy.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets which policy of the site it is.</summary>
    public LegalPolicyKind Kind { get; init; }

    /// <summary>Gets the number of the version in force.</summary>
    public int Version { get; init; }

    /// <summary>Gets the rich-text content of the version in force.</summary>
    public string Content { get; init; } = "{}";

    /// <summary>Gets when the version in force was published.</summary>
    public DateTimeOffset PublishedAt { get; init; }
}

/// <summary>
/// Version of a legal policy that a later one replaced, as the queries read it.
/// </summary>
public sealed class LegalPolicyVersionRow
{
    /// <summary>Gets the identifier of the policy.</summary>
    public Guid LegalPolicyId { get; init; }

    /// <summary>Gets the policy.</summary>
    public LegalPolicyRow LegalPolicy { get; init; } = null!;

    /// <summary>Gets the number of the version.</summary>
    public int Number { get; init; }

    /// <summary>Gets the rich-text content of the version.</summary>
    public string Content { get; init; } = "{}";

    /// <summary>Gets when the version was published.</summary>
    public DateTimeOffset PublishedAt { get; init; }
}
