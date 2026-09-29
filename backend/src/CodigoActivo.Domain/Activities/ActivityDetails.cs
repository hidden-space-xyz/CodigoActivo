namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Titles, place, modality and thumbnail of an activity as it is created or replaced.
/// </summary>
/// <param name="Title">Title; surrounding spaces are removed.</param>
/// <param name="Description">Description.</param>
/// <param name="Location">Place; surrounding spaces are removed.</param>
/// <param name="ActivityModalityTypeId">Identifier of the modality.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public sealed record ActivityDetails(
    string Title,
    string Description,
    string Location,
    Guid ActivityModalityTypeId,
    Guid ThumbnailId
);
