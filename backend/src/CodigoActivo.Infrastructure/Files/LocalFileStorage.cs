using CodigoActivo.Application.Abstractions.Storage;

namespace CodigoActivo.Infrastructure.Files;

/// <summary>
/// Stores and retrieves file content beneath the configured local storage root.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private const int StreamBufferSize = 64 * 1024;

    private readonly string rootPath;

    /// <summary>
    /// Initializes the local file storage with its required dependencies.
    /// </summary>
    /// <param name="options">Configuration values used by the component.</param>
    public LocalFileStorage(FileStorageOptions options)
    {
        var configured = string.IsNullOrWhiteSpace(options.RootPath) ? "files" : options.RootPath;
        var root = Path.GetFullPath(configured);

        Directory.CreateDirectory(root);

        rootPath = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Writes the content to a temporary entry of the root, flushes it to disk and only then moves
    /// it to its name, so a write that fails or is cancelled leaves nothing under the name and
    /// content already stored there is never replaced.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    /// <param name="content">Content stream to store or inspect.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="IOException">Content is already stored under the name, or it could not be written.</exception>
    public async Task SaveAsync(string storedName, Stream content, CancellationToken ct = default)
    {
        var path = ResolvePath(storedName);
        var temporaryPath = ResolvePath($".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (
                var fs = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    StreamBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan
                )
            )
            {
                await content.CopyToAsync(fs, ct);
                fs.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Lists the entries of the root last written at or before an instant.
    /// </summary>
    /// <param name="cutoff">Latest write instant to include.</param>
    /// <returns>The names of the entries.</returns>
    public IReadOnlyList<string> ListWrittenUpTo(DateTimeOffset cutoff)
    {
        return
        [
            .. new DirectoryInfo(rootPath)
                .EnumerateFiles()
                .Where(file => file.LastWriteTimeUtc <= cutoff.UtcDateTime)
                .Select(file => file.Name),
        ];
    }

    /// <summary>
    /// Opens the stored file as a read-only stream.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching stream, or <see langword="null"/> when it is not found.</returns>
    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default)
    {
        var path = ResolvePath(storedName);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        return Task.FromResult<Stream?>(stream);
    }

    /// <summary>
    /// Deletes the selected local file system.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    public void Delete(string storedName)
    {
        var path = ResolvePath(storedName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string ResolvePath(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName))
        {
            throw new ArgumentException("A stored file name is required.", nameof(storedName));
        }

        if (!string.Equals(storedName, Path.GetFileName(storedName), StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid stored file name.", nameof(storedName));
        }

        var fullPath = Path.GetFullPath(Path.Join(rootPath, storedName));
        return !fullPath.StartsWith(rootPath, StringComparison.Ordinal)
            ? throw new ArgumentException("Invalid stored file name.", nameof(storedName))
            : fullPath;
    }
}
