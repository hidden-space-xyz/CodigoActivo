using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// What a resource holds: an external resource is only a link, and a resource hosted on the site
/// is only a rich-text body.
/// </summary>
public sealed record ResourceContent
{
    private ResourceContent(RichText description, string? url)
    {
        Description = description;
        Url = url;
    }

    /// <summary>
    /// Gets the rich-text body, an empty document for external resources.
    /// </summary>
    public RichText Description { get; }

    /// <summary>
    /// Gets the link of an external resource.
    /// </summary>
    public string? Url { get; }

    /// <summary>
    /// Builds the content a resource of a type may hold.
    /// </summary>
    /// <param name="type">Type of the resource.</param>
    /// <param name="description">Rich-text body supplied.</param>
    /// <param name="url">Link supplied.</param>
    /// <returns>The content, or a validation error when it does not fit the type.</returns>
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

            return new ResourceContent(RichText.Empty, url.Trim());
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            return Error.Validation(DomainErrorCode.ResourceUrlNotAllowed);
        }

        if (description.IsEmpty)
        {
            return Error.Validation(DomainErrorCode.ResourceDescriptionRequired);
        }

        return new ResourceContent(description, null);
    }
}
