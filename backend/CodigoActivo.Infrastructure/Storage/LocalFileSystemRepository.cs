using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Infrastructure.Storage;

public sealed class LocalFileSystemRepository : ILocalFileSystemRepository
{
    private readonly string rootPath;

    public LocalFileSystemRepository(FileStorageOptions options)
    {
        var configured = string.IsNullOrWhiteSpace(options.RootPath) ? "files" : options.RootPath;
        var root = Path.GetFullPath(configured);

        Directory.CreateDirectory(root);

        rootPath = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
    }

    public async Task SaveAsync(string storedName, Stream content, CancellationToken ct = default)
    {
        var path = ResolvePath(storedName);
        await using var fs = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        );
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default)
    {
        var path = ResolvePath(storedName);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public void Delete(string storedName)
    {
        var path = ResolvePath(storedName);
        if (File.Exists(path)) File.Delete(path);
    }

    private string ResolvePath(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName))
            throw new ArgumentException("A stored file name is required.", nameof(storedName));

        if (!string.Equals(storedName, Path.GetFileName(storedName), StringComparison.Ordinal))
            throw new ArgumentException("Invalid stored file name.", nameof(storedName));

        var fullPath = Path.GetFullPath(Path.Combine(rootPath, storedName));
        return !fullPath.StartsWith(rootPath, StringComparison.Ordinal)
            ? throw new ArgumentException("Invalid stored file name.", nameof(storedName))
            : fullPath;
    }
}