using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// Uploaded file: its original name, the format detected from its content and who uploaded it.
/// Its content never changes; a new image is uploaded as a new file.
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
    /// Gets the original name, already sanitized.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the extension of the detected format.
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
    /// <param name="name">Original name, already sanitized.</param>
    /// <param name="extension">Extension of the detected format.</param>
    /// <param name="uploaderId">Identifier of the user who uploads it.</param>
    /// <param name="now">Current time.</param>
    /// <param name="id">Stable identifier for seeded files; a new one otherwise.</param>
    /// <returns>The new file.</returns>
    public static StoredFile Upload(
        string name,
        string extension,
        UserId uploaderId,
        DateTimeOffset now,
        StoredFileId? id = null
    )
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(extension);

        return new StoredFile
        {
            Id = id ?? StoredFileId.New(),
            Name = name,
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
    /// Marks the file as deleted, so whatever caches it can drop it once it is gone.
    /// </summary>
    public void Delete()
    {
        Raise(new StoredFileDeleted(Id));
    }
}
