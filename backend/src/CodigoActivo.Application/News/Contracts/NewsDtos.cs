namespace CodigoActivo.Application.News.Contracts;

/// <summary>
/// Contains the news item data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="Featured">Whether featured.</param>
public record NewsItemResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid ThumbnailId,
    bool Featured
)
{
    /// <summary>
    /// Initializes an empty news item response for serialization.
    /// </summary>
    public NewsItemResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            default,
            null,
            Guid.Empty,
            false
        ) { }
}

/// <summary>
/// Contains the compact news list item data returned in list results.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="Featured">Whether featured.</param>
public record NewsListItemResponse(
    Guid Id,
    string Title,
    string Subtitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid ThumbnailId,
    bool Featured
)
{
    /// <summary>
    /// Initializes an empty news list item response for serialization.
    /// </summary>
    public NewsListItemResponse()
        : this(Guid.Empty, string.Empty, string.Empty, default, null, Guid.Empty, false) { }
}
