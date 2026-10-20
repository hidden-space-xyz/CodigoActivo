using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Saves and deletes the stored content of every <see cref="FileVariant"/> of a file together.
/// </summary>
public static class StoredVariants
{
    /// <summary>
    /// Saves every variant of a processed image; when one cannot be saved, the variants this call
    /// saved are discarded before the failure is rethrown.
    /// </summary>
    /// <param name="storage">Storage holding the content of the files.</param>
    /// <param name="id">Identifier of the file the variants belong to.</param>
    /// <param name="image">Encoded variants to save.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task SaveVariantsAsync(
        this IFileStorage storage,
        StoredFileId id,
        ProcessedImage image,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(image);

        var saved = new List<string>();
        try
        {
            foreach (var variant in Enum.GetValues<FileVariant>())
            {
                var storedName = FileNaming.StoredName(
                    id,
                    ProcessedImage.Format.Extension,
                    variant
                );
                await using var content = new MemoryStream(image.Of(variant), writable: false);
                await storage.SaveAsync(storedName, content, ct);
                saved.Add(storedName);
            }
        }
        catch
        {
            Discard(storage, saved);
            throw;
        }
    }

    /// <summary>
    /// Deletes the stored content of every variant of a file.
    /// </summary>
    /// <param name="storage">Storage holding the content of the files.</param>
    /// <param name="id">Identifier of the file.</param>
    /// <param name="extension">Extension of the file.</param>
    public static void DeleteVariants(this IFileStorage storage, StoredFileId id, string extension)
    {
        ArgumentNullException.ThrowIfNull(storage);

        foreach (var storedName in FileNaming.StoredNames(id, extension))
        {
            storage.Delete(storedName);
        }
    }

    /// <summary>
    /// Deletes the stored content of every variant of a file while a failure is being handled, so
    /// that failure is the one rethrown; content that cannot be deleted is left to
    /// <see cref="Commands.PurgeUnusedFilesCommand"/>.
    /// </summary>
    /// <param name="storage">Storage holding the content of the files.</param>
    /// <param name="id">Identifier of the file.</param>
    /// <param name="extension">Extension of the file.</param>
    public static void DiscardVariants(this IFileStorage storage, StoredFileId id, string extension)
    {
        ArgumentNullException.ThrowIfNull(storage);

        Discard(storage, FileNaming.StoredNames(id, extension));
    }

    private static void Discard(IFileStorage storage, IEnumerable<string> storedNames)
    {
        foreach (var storedName in storedNames)
        {
            try
            {
                storage.Delete(storedName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
