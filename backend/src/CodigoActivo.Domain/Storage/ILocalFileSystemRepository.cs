namespace CodigoActivo.Domain.Storage;

public interface ILocalFileSystemRepository
{
    Task SaveAsync(string storedName, Stream content, CancellationToken ct = default);

    Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default);

    void Delete(string storedName);
}
