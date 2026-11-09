using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Checks, before content is saved, that its thumbnail and every file its rich text embeds exist.
/// </summary>
public static class ContentFiles
{
    /// <summary>
    /// Checks that the thumbnail and the embedded files of the content exist.
    /// </summary>
    /// <param name="files">Repository used to look the files up.</param>
    /// <param name="thumbnailId">Thumbnail of the content.</param>
    /// <param name="description">Rich text of the content.</param>
    /// <param name="thumbnailMissing">Error code when the thumbnail no longer exists.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is a success, <paramref name="thumbnailMissing"/> or
    /// <see cref="ApplicationErrorCode.RichTextFileNotFound"/>.
    /// </returns>
    public static async Task<Result> EnsureAvailableAsync(
        IStoredFileRepository files,
        StoredFileId thumbnailId,
        RichText? description,
        ApplicationErrorCode thumbnailMissing,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(files);

        var embedded = RichTextFileReferences.Extract(description);
        var found = (await files.ListExistingAsync([thumbnailId, .. embedded], ct)).ToHashSet();
        if (!found.Contains(thumbnailId))
        {
            return Error.Validation(thumbnailMissing);
        }

        return embedded.All(found.Contains)
            ? Result.Success()
            : Error.Validation(ApplicationErrorCode.RichTextFileNotFound);
    }
}
