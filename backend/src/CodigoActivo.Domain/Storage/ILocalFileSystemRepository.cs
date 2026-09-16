namespace CodigoActivo.Domain.Storage;

/// <summary>
/// Persists and retrieves local file system data from the database.
/// </summary>
public interface ILocalFileSystemRepository
{
    /// <summary>
    /// Writes the supplied content to its durable storage location.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    /// <param name="content">Content stream to store or inspect.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SaveAsync(string storedName, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Opens the stored file as a read-only stream.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching stream, or <see langword="null"/> when it is not found.</returns>
    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default);

    /// <summary>
    /// Deletes the selected local file system.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    public void Delete(string storedName);
}
