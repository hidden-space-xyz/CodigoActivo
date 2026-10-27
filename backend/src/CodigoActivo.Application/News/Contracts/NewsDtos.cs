namespace CodigoActivo.Application.News.Contracts;

/// <summary>
/// Contains the news item data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the news item.</param>
/// <param name="Title">Headline.</param>
/// <param name="Subtitle">Line shown below the headline.</param>
/// <param name="Description">Body as rich text JSON.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
/// <param name="Featured">Whether this is the featured news item.</param>
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
/// <param name="Id">Identifier of the news item.</param>
/// <param name="Title">Headline.</param>
/// <param name="Subtitle">Line shown below the headline.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
/// <param name="Featured">Whether this is the featured news item.</param>
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
