using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Title, description, place, modality and thumbnail of an activity as it is created or replaced.
/// </summary>
/// <param name="Title">Title; surrounding spaces are removed.</param>
/// <param name="Description">Plain-text description, stored as given.</param>
/// <param name="Location">Place; surrounding spaces are removed.</param>
/// <param name="Modality">Whether the activity takes place in person or online.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
public sealed record ActivityDetails(
    string Title,
    string Description,
    string Location,
    ActivityModality Modality,
    StoredFileId ThumbnailId
);
