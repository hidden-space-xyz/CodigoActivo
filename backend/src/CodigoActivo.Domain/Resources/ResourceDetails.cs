using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// Titles and thumbnail of a resource as it is created or replaced.
/// </summary>
/// <param name="Title">Title; surrounding spaces are removed.</param>
/// <param name="Subtitle">Subtitle; surrounding spaces are removed.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public sealed record ResourceDetails(string Title, string Subtitle, StoredFileId ThumbnailId);
