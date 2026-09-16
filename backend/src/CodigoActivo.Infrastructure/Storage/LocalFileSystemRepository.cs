using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Infrastructure.Storage;

/// <summary>
/// Stores and retrieves file content beneath the configured local storage root.
/// </summary>
public sealed class LocalFileSystemRepository : ILocalFileSystemRepository
{
    private const int StreamBufferSize = 64 * 1024;

    private readonly string rootPath;

    /// <summary>
    /// Initializes a local file system repository with its required dependencies.
    /// </summary>
    /// <param name="options">Configuration values used by the component.</param>
    public LocalFileSystemRepository(FileStorageOptions options)
    {
        var configured = string.IsNullOrWhiteSpace(options.RootPath) ? "files" : options.RootPath;
        var root = Path.GetFullPath(configured);

        Directory.CreateDirectory(root);

        rootPath = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Writes the supplied content to its durable storage location.
    /// </summary>
    /// <param name="storedName">Storage-safe file name identifying the content.</param>
    /// <param name="content">Content stream to store or inspect.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SaveAsync(string storedName, Stream content, CancellationToken ct = default)
    {
        var path = ResolvePath(storedName);
        await using var fs = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            StreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        await content.CopyToAsync(fs, ct);
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
