using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// Works out which files an aggregate with a thumbnail and a rich text description stops
/// referencing.
/// </summary>
public static class ReleasedFiles
{
    /// <summary>
    /// Gets the files the previous thumbnail and description referenced and the current ones do not.
    /// </summary>
    /// <param name="previousThumbnailId">Thumbnail before the change.</param>
    /// <param name="thumbnailId">Thumbnail after the change.</param>
    /// <param name="previousDescription">Description before the change.</param>
    /// <param name="description">Description after the change.</param>
    /// <returns>The identifiers of the files no longer referenced.</returns>
    public static IReadOnlyCollection<StoredFileId> Between(
        StoredFileId previousThumbnailId,
        StoredFileId thumbnailId,
        RichText previousDescription,
        RichText description
    )
    {
        var released = RichTextFileReferences
            .ExtractRemoved(previousDescription, description)
            .ToHashSet();
        if (previousThumbnailId != thumbnailId)
        {
            released.Add(previousThumbnailId);
        }

        released.Remove(thumbnailId);
        return released;
    }

    /// <summary>
    /// Gets every file a thumbnail and a description reference.
    /// </summary>
    /// <param name="thumbnailId">Thumbnail.</param>
    /// <param name="description">Description.</param>
    /// <returns>The identifiers of the files referenced.</returns>
    public static IReadOnlyCollection<StoredFileId> Of(
        StoredFileId thumbnailId,
        RichText description
    )
    {
        var released = RichTextFileReferences.Extract(description).ToHashSet();
        released.Add(thumbnailId);
        return released;
    }
}
