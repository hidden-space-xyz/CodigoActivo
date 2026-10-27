namespace CodigoActivo.Application.Resources.Contracts;

/// <summary>
/// Contains the resource type data returned by the API.
/// </summary>
/// <param name="Id">Catalog identifier of the type.</param>
/// <param name="Name">Display name of the type.</param>
/// <param name="Description">Description of the type shown to people.</param>
/// <param name="Color">Color the type is shown with.</param>
/// <param name="IsExternal">
/// Whether resources of this type link to an external page instead of carrying a body.
/// </param>
public record ResourceTypeResponse(
    Guid Id,
    string Name,
    string Description,
    string Color,
    bool IsExternal
)
{
    /// <summary>
    /// Initializes an empty resource type response for serialization.
    /// </summary>
    public ResourceTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty, false) { }
}

/// <summary>
/// Contains the resource data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the resource.</param>
/// <param name="Title">Title of the resource.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="Description">
/// Body as rich text JSON; an empty document for an external resource.
/// </param>
/// <param name="Url">
/// Link of an external resource; <see langword="null"/> for an internal one.
/// </param>
/// <param name="Type">Type of the resource.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public record ResourceResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    string? Url,
    ResourceTypeResponse Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid ThumbnailId
)
{
    /// <summary>
    /// Initializes an empty resource response for serialization.
    /// </summary>
    public ResourceResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            new ResourceTypeResponse(),
            default,
            null,
            Guid.Empty
        ) { }
}

/// <summary>
/// Contains the compact resource list item data returned in list results.
/// </summary>
/// <param name="Id">Identifier of the resource.</param>
/// <param name="Title">Title of the resource.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="Url">
/// Link of an external resource; <see langword="null"/> for an internal one.
/// </param>
/// <param name="Type">Type of the resource.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public record ResourceListItemResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string? Url,
    ResourceTypeResponse Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid ThumbnailId
)
{
    /// <summary>
    /// Initializes an empty resource list item response for serialization.
    /// </summary>
    public ResourceListItemResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            null,
            new ResourceTypeResponse(),
            default,
            null,
            Guid.Empty
        ) { }
}
