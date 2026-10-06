using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Titles, description and thumbnail of an event as it is created or replaced.
/// </summary>
/// <param name="Title">Title; surrounding spaces are removed.</param>
/// <param name="Subtitle">Subtitle; surrounding spaces are removed.</param>
/// <param name="Description">Rich-text description.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public sealed record EventContent(
    string Title,
    string Subtitle,
    RichText Description,
    StoredFileId ThumbnailId
);
