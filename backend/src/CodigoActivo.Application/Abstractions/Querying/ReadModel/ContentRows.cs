namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Stored resource as the queries read it.
/// </summary>
public sealed class ResourceRow
{
    /// <summary>Gets the identifier of the resource.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the subtitle.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Gets the rich-text description of a resource hosted on the site.</summary>
    public string Description { get; init; } = "{}";

    /// <summary>Gets the link of an external resource.</summary>
    public string? Url { get; init; }

    /// <summary>Gets the identifier of the resource type.</summary>
    public Guid ResourceTypeId { get; init; }

    /// <summary>Gets the resource type.</summary>
    public ResourceTypeRow ResourceType { get; init; } = null!;

    /// <summary>Gets the identifier of the thumbnail file.</summary>
    public Guid ThumbnailId { get; init; }

    /// <summary>Gets when the resource was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the resource was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Gets the identifier of the author.</summary>
    public Guid CreatedBy { get; init; }

    /// <summary>Gets the identifier of the last editor.</summary>
    public Guid? UpdatedBy { get; init; }
}

/// <summary>
/// Stored resource type as the queries read it.
/// </summary>
public sealed class ResourceTypeRow
{
    /// <summary>Gets the identifier of the type.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the display color.</summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether resources of this type are external links.</summary>
    public bool IsExternal { get; init; }
}

/// <summary>
/// Stored news item as the queries read it.
/// </summary>
public sealed class NewsItemRow
{
    /// <summary>Gets the identifier of the news item.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the subtitle.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Gets the rich-text description.</summary>
    public string Description { get; init; } = "{}";

    /// <summary>Gets a value indicating whether the news item is featured.</summary>
    public bool Featured { get; init; }

    /// <summary>Gets the identifier of the thumbnail file.</summary>
    public Guid ThumbnailId { get; init; }

    /// <summary>Gets when the news item was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the news item was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Gets the identifier of the author.</summary>
    public Guid CreatedBy { get; init; }

    /// <summary>Gets the identifier of the last editor.</summary>
    public Guid? UpdatedBy { get; init; }
}

/// <summary>
/// Stored partner as the queries read it.
/// </summary>
public sealed class PartnerRow
{
    /// <summary>Gets the identifier of the partner.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the date the partnership started.</summary>
    public DateOnly FromDate { get; init; }

    /// <summary>Gets the sponsorship tier.</summary>
    public int Tier { get; init; }

    /// <summary>Gets the website.</summary>
    public string? Web { get; init; }

    /// <summary>Gets the identifier of the logo file.</summary>
    public Guid ThumbnailId { get; init; }

    /// <summary>Gets when the partner was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the partner was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Gets the identifier of the author.</summary>
    public Guid CreatedBy { get; init; }

    /// <summary>Gets the identifier of the last editor.</summary>
    public Guid? UpdatedBy { get; init; }
}

/// <summary>
/// Stored file metadata as the queries read it.
/// </summary>
public sealed class FileRow
{
    /// <summary>Gets the identifier of the file.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the original name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the extension.</summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>Gets when the file was uploaded.</summary>
    public DateTimeOffset UploadedAt { get; init; }

    /// <summary>Gets the identifier of the uploader.</summary>
    public Guid UploadedBy { get; init; }
}
