using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// What a resource holds: an external resource is only a link, and a resource hosted on the site
/// is only a rich-text body.
/// </summary>
public sealed record ResourceContent
{
    private ResourceContent(string description, string? url)
    {
        Description = description;
        Url = url;
    }

    /// <summary>
    /// Gets the rich-text body, an empty document for external resources.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the link of an external resource.
    /// </summary>
    public string? Url { get; }

    /// <summary>
    /// Builds the content a resource of a type may hold.
    /// </summary>
    /// <param name="isExternal">Whether the type of the resource is an external link.</param>
    /// <param name="description">Rich-text body supplied.</param>
    /// <param name="url">Link supplied.</param>
    /// <returns>The content, or a validation error when it does not fit the type.</returns>
    public static Result<ResourceContent> For(bool isExternal, string? description, string? url)
    {
        if (isExternal)
        {
            if (!RichTextDocument.IsEmpty(description))
            {
                return Error.Validation(ErrorCode.ResourceDescriptionNotAllowed);
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return Error.Validation(ErrorCode.ResourceUrlRequired);
            }

            return new ResourceContent("{}", url.Trim());
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            return Error.Validation(ErrorCode.ResourceUrlNotAllowed);
        }

        if (RichTextDocument.IsEmpty(description))
        {
            return Error.Validation(ErrorCode.ResourceDescriptionRequired);
        }

        return new ResourceContent(description!, null);
    }
}
