using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the announcement data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="CreatedBy">The created by value.</param>
/// <param name="UpdatedBy">The updated by value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="Featured">Whether featured.</param>
public record AnnouncementResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId,
    bool Featured
)
{
    /// <summary>
    /// Initializes an empty announcement response for serialization.
    /// </summary>
    public AnnouncementResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            default,
            null,
            Guid.Empty,
            null,
            Guid.Empty,
            false
        ) { }
}

/// <summary>
/// Contains the compact announcement list item data returned in list results.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="CreatedBy">The created by value.</param>
/// <param name="UpdatedBy">The updated by value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="Featured">Whether featured.</param>
public record AnnouncementListItemResponse(
    Guid Id,
    string Title,
    string Subtitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    Guid ThumbnailId,
    bool Featured
)
{
    /// <summary>
    /// Initializes an empty announcement list item response for serialization.
    /// </summary>
    public AnnouncementListItemResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            default,
            null,
            Guid.Empty,
            null,
            Guid.Empty,
            false
        ) { }
}

/// <summary>
/// Contains the client-supplied data used to create an announcement.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record CreateAnnouncementRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] [MaxLength(262144)] string Description,
    Guid ThumbnailId
);

/// <summary>
/// Contains the client-supplied data used to update the announcement.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record UpdateAnnouncementRequest(
    [Required] [MaxLength(200)] [NotBlank] string Title,
    [Required] [MaxLength(300)] [NotBlank] string Subtitle,
    [JsonString] [MaxLength(262144)] string Description,
    Guid ThumbnailId
);
