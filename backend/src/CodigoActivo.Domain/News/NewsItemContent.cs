using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.News;

/// <summary>
/// Content of a news item as it is created or replaced.
/// </summary>
/// <param name="Title">Title; surrounding spaces are removed.</param>
/// <param name="Subtitle">Subtitle; surrounding spaces are removed.</param>
/// <param name="Description">Rich-text body.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public sealed record NewsItemContent(
    string Title,
    string Subtitle,
    RichText Description,
    StoredFileId ThumbnailId
);
