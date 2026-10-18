using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Checks, inside the transaction that saves content, that its thumbnail and every file its rich
/// text embeds still exist, and keeps them until the content is saved. The purge of unused uploads
/// locks the files it deletes, so it either waits for the save and then sees the content using
/// them, or deletes them first and the save is refused instead of keeping a broken reference.
/// </summary>
public static class ContentFiles
{
    /// <summary>
    /// Locks the thumbnail and the embedded files of the content and checks that they exist.
    /// </summary>
    /// <param name="files">Repository used to lock the files.</param>
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
        var found = (await files.LockExistingAsync([thumbnailId, .. embedded], ct)).ToHashSet();
        if (!found.Contains(thumbnailId))
        {
            return Error.Validation(thumbnailMissing);
        }

        return embedded.All(found.Contains)
            ? Result.Success()
            : Error.Validation(ApplicationErrorCode.RichTextFileNotFound);
    }
}
