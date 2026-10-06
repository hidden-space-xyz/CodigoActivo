using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// Uploaded file: its original name, the format detected from its content and who uploaded it.
/// Replacing the content keeps the identity and the uploader.
/// </summary>
public class StoredFile : AggregateRoot<StoredFileId>
{
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
    /// Gets when the current content was uploaded.
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
        var file = new StoredFile { Id = id ?? StoredFileId.New(), UploadedBy = uploaderId };
        file.Apply(name, extension, now);
        return file;
    }

    /// <summary>
    /// Records new content for the file.
    /// </summary>
    /// <param name="name">Original name of the new content, already sanitized.</param>
    /// <param name="extension">Extension of the detected format.</param>
    /// <param name="now">Current time.</param>
    public void Replace(string name, string extension, DateTimeOffset now)
    {
        Apply(name, extension, now);
        Raise(new StoredFileReplaced(Id));
    }

    /// <summary>
    /// Marks the file as deleted, so whatever caches it can drop it once it is gone.
    /// </summary>
    public void Delete()
    {
        Raise(new StoredFileDeleted(Id));
    }

    private void Apply(string name, string extension, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(extension);

        Name = name;
        Extension = extension;
        UploadedAt = now;
    }
}
