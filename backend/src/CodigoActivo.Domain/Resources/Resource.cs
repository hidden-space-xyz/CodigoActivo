using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// Resource offered on the site, either hosted as rich text or linked from outside. Its content
/// always fits its type and changes only as a whole, recording who made the change.
/// </summary>
public class Resource : AuditableEntity<ResourceId>
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
    public RichText Description { get; private set; } = RichText.Empty;

    /// <summary>
    /// Gets the link of an external resource.
    /// </summary>
    public string? Url { get; private set; }

    /// <summary>
    /// Gets whether the resource is written on the site or links elsewhere.
    /// </summary>
    public ResourceType ResourceType { get; private set; }

    /// <summary>
    /// Gets the identifier of the thumbnail file.
    /// </summary>
    public StoredFileId ThumbnailId { get; private set; }

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
        UserId authorId,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(content);

        var resource = new Resource();
        resource.Apply(details, content);
        resource.RecordCreation(authorId, now);
        resource.Raise(new ResourceCreated(resource.Id));
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
        UserId editorId,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(content);

        var previousThumbnailId = ThumbnailId;
        var previousDescription = Description;
        Apply(details, content);
        RecordUpdate(editorId, now);
        Raise(
            new ResourceUpdated(
                Id,
                ReleasedFiles.Between(
                    previousThumbnailId,
                    ThumbnailId,
                    previousDescription,
                    Description
                )
            )
        );
    }

    /// <summary>
    /// Marks the resource as deleted, so the files it references can be released once it is gone.
    /// </summary>
    public void Delete()
    {
        Raise(new ResourceDeleted(Id, ReleasedFiles.Of(ThumbnailId, Description)));
    }

    private void Apply(ResourceDetails details, ResourceContent content)
    {
        Title = details.Title.Trim();
        Subtitle = details.Subtitle.Trim();
        ResourceType = details.ResourceType;
        ThumbnailId = details.ThumbnailId;
        Description = content.Description;
        Url = content.Url;
    }
}
