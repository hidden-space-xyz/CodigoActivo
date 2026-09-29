using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// Resource offered on the site, either hosted as rich text or linked from outside. Its content
/// always fits its type and changes only as a whole, recording who made the change.
/// </summary>
public class Resource : AuditableEntity, IAggregateRoot
{
    private Resource() { }

    /// <summary>
    /// Gets the title displayed to users.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the supporting subtitle displayed to users.
    /// </summary>
    public string Subtitle { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the rich-text body of a resource hosted on the site.
    /// </summary>
    public string Description { get; private set; } = "{}";

    /// <summary>
    /// Gets the link of an external resource.
    /// </summary>
    public string? Url { get; private set; }

    /// <summary>
    /// Gets the identifier of the resource type.
    /// </summary>
    public Guid ResourceTypeId { get; private set; }

    /// <summary>
    /// Gets the identifier of the thumbnail file.
    /// </summary>
    public Guid ThumbnailId { get; private set; }

    /// <summary>
    /// Creates a resource.
    /// </summary>
    /// <param name="details">Titles, type and thumbnail of the resource.</param>
    /// <param name="content">Content that fits the type.</param>
    /// <param name="authorId">Identifier of the user who creates it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The new resource.</returns>
    public static Resource Create(
        ResourceDetails details,
        ResourceContent content,
        Guid authorId,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(content);

        var resource = new Resource();
        resource.Apply(details, content);
        resource.RecordCreation(authorId, now);
        return resource;
    }

    /// <summary>
    /// Replaces the resource.
    /// </summary>
    /// <param name="details">New titles, type and thumbnail.</param>
    /// <param name="content">New content that fits the type.</param>
    /// <param name="editorId">Identifier of the user who edits it.</param>
    /// <param name="now">Current time.</param>
    public void Update(
        ResourceDetails details,
        ResourceContent content,
        Guid editorId,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(content);

        Apply(details, content);
        RecordUpdate(editorId, now);
    }

    private void Apply(ResourceDetails details, ResourceContent content)
    {
        Title = details.Title.Trim();
        Subtitle = details.Subtitle.Trim();
        ResourceTypeId = details.ResourceTypeId;
        ThumbnailId = details.ThumbnailId;
        Description = content.Description;
        Url = content.Url;
    }
}
