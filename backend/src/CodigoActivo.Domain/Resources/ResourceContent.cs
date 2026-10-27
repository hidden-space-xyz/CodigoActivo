using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// What a resource holds: an external resource is only a link, and a resource hosted on the site
/// is only a rich-text body.
/// </summary>
public sealed record ResourceContent
{
    private ResourceContent(ResourceType type, RichText description, string? url)
    {
        Type = type;
        Description = description;
        Url = url;
    }

    /// <summary>
    /// Gets the type this content was checked against: written on the site or linking elsewhere.
    /// </summary>
    public ResourceType Type { get; }

    /// <summary>
    /// Gets the rich-text body, an empty document for external resources.
    /// </summary>
    public RichText Description { get; }

    /// <summary>
    /// Gets the trimmed link of an external resource, or <see langword="null"/> for a resource
    /// hosted on the site.
    /// </summary>
    public string? Url { get; }

    /// <summary>
    /// Builds the content a resource of a type may hold.
    /// </summary>
    /// <param name="type">Type of the resource.</param>
    /// <param name="description">Rich-text body supplied.</param>
    /// <param name="url">Link supplied; blank means none.</param>
    /// <returns>
    /// The content, or, for an external resource,
    /// <see cref="DomainErrorCode.ResourceDescriptionNotAllowed"/> when it has a body and
    /// <see cref="DomainErrorCode.ResourceUrlRequired"/> when it has no link; for a resource hosted
    /// on the site, <see cref="DomainErrorCode.ResourceUrlNotAllowed"/> when it has a link and
    /// <see cref="DomainErrorCode.ResourceDescriptionRequired"/> when its body is empty.
    /// </returns>
    public static Result<ResourceContent> For(ResourceType type, RichText description, string? url)
    {
        ArgumentNullException.ThrowIfNull(description);

        if (type is ResourceType.External)
        {
            if (!description.IsEmpty)
            {
                return Error.Validation(DomainErrorCode.ResourceDescriptionNotAllowed);
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return Error.Validation(DomainErrorCode.ResourceUrlRequired);
            }

            return new ResourceContent(type, RichText.Empty, url.Trim());
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            return Error.Validation(DomainErrorCode.ResourceUrlNotAllowed);
        }

        if (description.IsEmpty)
        {
            return Error.Validation(DomainErrorCode.ResourceDescriptionRequired);
        }

        return new ResourceContent(type, description, null);
    }
}
