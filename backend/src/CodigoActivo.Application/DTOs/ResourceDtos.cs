using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the resource type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Color">The color value.</param>
/// <param name="IsExternal">Whether external.</param>
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
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Url">The url value.</param>
/// <param name="Type">The type value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="CreatedBy">The created by value.</param>
/// <param name="UpdatedBy">The updated by value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record ResourceResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    string? Url,
    ResourceTypeResponse Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
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
            Guid.Empty,
            null,
            Guid.Empty
        ) { }
}

/// <summary>
/// Contains the compact resource list item data returned in list results.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Url">The url value.</param>
/// <param name="Type">The type value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="CreatedBy">The created by value.</param>
/// <param name="UpdatedBy">The updated by value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record ResourceListItemResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string? Url,
    ResourceTypeResponse Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
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
            Guid.Empty,
            null,
            Guid.Empty
        ) { }
}

/// <summary>
/// Contains the client-supplied data used to create a resource.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Url">The url value.</param>
/// <param name="ResourceTypeId">Identifier of the resource type.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record CreateResourceRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] [MaxLength(262144)] string? Description,
    [HttpUrl] [MaxLength(500)] string? Url,
    Guid ResourceTypeId,
    Guid ThumbnailId
);

/// <summary>
/// Contains the client-supplied data used to update the resource.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Url">The url value.</param>
/// <param name="ResourceTypeId">Identifier of the resource type.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record UpdateResourceRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] [MaxLength(262144)] string? Description,
    [HttpUrl] [MaxLength(500)] string? Url,
    Guid ResourceTypeId,
    Guid ThumbnailId
);
