namespace CodigoActivo.Application.Abstractions.Storage;

/// <summary>
/// Stores the binary content of uploaded files, keyed by their storage-safe name.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Writes the supplied content to its durable storage location. The content appears under its
    /// name only once it is complete and on disk, and never replaces content already stored there.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    /// <param name="content">Content stream to store or inspect.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="IOException">Content is already stored under the name, or it could not be written.</exception>
    public Task SaveAsync(string storedName, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Lists every stored entry last written at or before an instant, including the temporary ones
    /// a write left behind.
    /// </summary>
    /// <param name="cutoff">Latest write instant to include.</param>
    /// <returns>The names of the entries.</returns>
    public IReadOnlyList<string> ListWrittenUpTo(DateTimeOffset cutoff);

    /// <summary>
    /// Opens the stored file as a read-only stream.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching stream, or <see langword="null"/> when it is not found.</returns>
    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default);

    /// <summary>
    /// Deletes the stored content, if any.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    public void Delete(string storedName);
}
