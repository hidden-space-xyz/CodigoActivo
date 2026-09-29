namespace CodigoActivo.Application.Abstractions.Persistence;

/// <summary>
/// Commits the changes staged by a use case as one unit.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Commits every staged change.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of rows written.</returns>
    /// <exception cref="UniqueConstraintViolationException">A unique rule of the database rejected the commit.</exception>
    public Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs work inside a database transaction and commits it when the work succeeds. When the
    /// database aborts the transaction because of a deadlock, the work runs again from the start;
    /// entities the failed attempt staged as new are dropped first. Inside a transaction already
    /// open the work just runs.
    /// </summary>
    /// <typeparam name="T">Type of the result of the work.</typeparam>
    /// <param name="work">Work to run; it receives the cancellation token.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the result of the work.</returns>
    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken ct = default
    );
}
