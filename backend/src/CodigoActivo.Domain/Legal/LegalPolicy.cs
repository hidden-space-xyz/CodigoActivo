using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Legal;

/// <summary>
/// A policy the site publishes, such as the privacy policy, written by the administrators as rich
/// text. There is at most one policy of each <see cref="Kind"/>. Every revision that changes its
/// content publishes a new <see cref="Version"/> and keeps the one it replaces in
/// <see cref="PreviousVersions"/>, so the text in force on any date can always be shown.
/// </summary>
public class LegalPolicy : AggregateRoot<LegalPolicyId>
{
    /// <summary>
    /// Number of the version a policy starts with.
    /// </summary>
    public const int FirstVersion = 1;

    private readonly List<LegalPolicyVersion> previousVersions = [];

    private LegalPolicy() { }

    /// <summary>
    /// Gets which policy of the site this is; it never changes.
    /// </summary>
    public LegalPolicyKind Kind { get; private set; }

    /// <summary>
    /// Gets the number of the version in force.
    /// </summary>
    public int Version { get; private set; } = FirstVersion;

    /// <summary>
    /// Gets the rich-text content of the version in force.
    /// </summary>
    public RichText Content { get; private set; } = RichText.Empty;

    /// <summary>
    /// Gets when the version in force was published.
    /// </summary>
    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>
    /// Gets the versions later revisions replaced.
    /// </summary>
    public IReadOnlyCollection<LegalPolicyVersion> PreviousVersions => previousVersions;

    /// <summary>
    /// Publishes the first version of a policy.
    /// </summary>
    /// <param name="kind">Which policy of the site it is.</param>
    /// <param name="content">Rich-text content; it must hold some text.</param>
    /// <param name="publishedAt">When it is published.</param>
    /// <returns>The policy, or a validation error when the content is empty.</returns>
    public static Result<LegalPolicy> Publish(
        LegalPolicyKind kind,
        RichText content,
        DateTimeOffset publishedAt
    )
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.IsEmpty)
        {
            return Error.Validation(DomainErrorCode.LegalPolicyContentRequired);
        }

        var policy = new LegalPolicy
        {
            Kind = kind,
            Content = content,
            PublishedAt = publishedAt,
        };
        policy.Raise(new LegalPolicyPublished(policy.Id));
        return policy;
    }

    /// <summary>
    /// Publishes a new content. When it differs from the version in force, that version is kept in
    /// <see cref="PreviousVersions"/> and the new one gets the next number; the same content
    /// changes nothing.
    /// </summary>
    /// <param name="content">New rich-text content; it must hold some text.</param>
    /// <param name="publishedAt">When it is published.</param>
    /// <returns>A success, or a validation error when the content is empty.</returns>
    public Result Revise(RichText content, DateTimeOffset publishedAt)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.IsEmpty)
        {
            return Error.Validation(DomainErrorCode.LegalPolicyContentRequired);
        }

        if (Content.HasSameContentAs(content))
        {
            return Result.Success();
        }

        previousVersions.Add(new LegalPolicyVersion(this));
        Version++;
        Content = content;
        PublishedAt = publishedAt;
        Raise(new LegalPolicyPublished(Id));
        return Result.Success();
    }
}
