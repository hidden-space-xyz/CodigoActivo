namespace CodigoActivo.Domain.Repositories;

/// <summary>
/// Defines the operations required to work with unit of work.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes as a single unit of work.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an int.</returns>
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
