namespace CodigoActivo.Domain.Resources;

/// <summary>
/// Titles, type and thumbnail of a resource as it is created or replaced.
/// </summary>
/// <param name="Title">Title; surrounding spaces are removed.</param>
/// <param name="Subtitle">Subtitle; surrounding spaces are removed.</param>
/// <param name="ResourceTypeId">Identifier of the resource type.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public sealed record ResourceDetails(
    string Title,
    string Subtitle,
    Guid ResourceTypeId,
    Guid ThumbnailId
);
