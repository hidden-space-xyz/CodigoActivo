using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// Uploaded file: the format its content is stored in and who uploaded it. It keeps no name, since
/// the one it was uploaded with may name the people it shows; its identifier names it. Its content
/// never changes; a new image is uploaded as a new file.
/// </summary>
public class StoredFile : AggregateRoot<StoredFileId>
{
    /// <summary>
    /// Days an upload that no content references is kept, so a form or an edit left open for a
    /// while can still save it; after that it is deleted, and a later save that still references it
    /// is refused.
    /// </summary>
    public const int UnusedRetentionDays = 7;

    private StoredFile() { }

    /// <summary>
    /// Gets the extension, without a leading dot, of the format the content is stored in, which
    /// every upload is re-encoded to.
    /// </summary>
    public string Extension { get; private set; } = string.Empty;

    /// <summary>
    /// Gets when the file was uploaded.
    /// </summary>
    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the user who uploaded the file.
    /// </summary>
    public UserId UploadedBy { get; private set; }

    /// <summary>
    /// Records a new upload.
    /// </summary>
    /// <param name="extension">Extension of the format the content is stored in.</param>
    /// <param name="uploaderId">Identifier of the user who uploads it.</param>
    /// <param name="now">Current time.</param>
    /// <param name="id">Stable identifier for seeded files; a new one otherwise.</param>
    /// <returns>The new file.</returns>
    public static StoredFile Upload(
        string extension,
        UserId uploaderId,
        DateTimeOffset now,
        StoredFileId? id = null
    )
    {
        ArgumentNullException.ThrowIfNull(extension);

        return new StoredFile
        {
            Id = id ?? StoredFileId.New(),
            Extension = extension,
            UploadedAt = now,
            UploadedBy = uploaderId,
        };
    }

    /// <summary>
    /// Gets the last upload instant of a file that is old enough to delete when no content
    /// references it.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns><paramref name="now"/> minus <see cref="UnusedRetentionDays"/>.</returns>
    public static DateTimeOffset UnusedPurgeCutoff(DateTimeOffset now)
    {
        return now.AddDays(-UnusedRetentionDays);
    }

    /// <summary>
    /// Raises <see cref="StoredFileDeleted"/> before the file is removed, so whatever caches it can
    /// drop it once it is gone.
    /// </summary>
    public void Delete()
    {
        Raise(new StoredFileDeleted(Id));
    }
}
