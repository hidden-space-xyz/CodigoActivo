namespace CodigoActivo.Domain.Storage;

public interface ILocalFileSystemRepository
{
    public Task SaveAsync(string storedName, Stream content, CancellationToken ct = default);

    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default);

    public void Delete(string storedName);
}
