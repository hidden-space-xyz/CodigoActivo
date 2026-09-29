using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// Stores and loads resources.
/// </summary>
public interface IResourceRepository : IRepository<Resource>
{
    /// <summary>
    /// Loads a resource to change it.
    /// </summary>
    /// <param name="id">Identifier of the resource.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the resource, or <see langword="null"/> when it does not exist.</returns>
    public Task<Resource?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
