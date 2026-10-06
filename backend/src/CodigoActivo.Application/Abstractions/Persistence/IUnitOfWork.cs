namespace CodigoActivo.Application.Abstractions.Persistence;

/// <summary>
/// Commits the changes staged by a use case as one unit.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Commits every staged change. When the database refuses the commit, the changes stay staged
    /// with the domain events they raised, so a later commit that includes them publishes them.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of rows written.</returns>
    /// <exception cref="UniqueConstraintViolationException">A unique rule of the database rejected the commit.</exception>
    public Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Forgets every change staged since the last commit, with the domain events it raised, so a
    /// use case that gives up after the database refused a commit leaves nothing for a later one.
    /// </summary>
    public void DiscardChanges();

    /// <summary>
    /// Runs work inside a database transaction and commits it when the work succeeds. When the work
    /// fails, the unit of work returns to the state it had before the attempt: the entities tracked
    /// since are dropped, the others get back the values and state they had, and the domain events
    /// raised during the attempt are discarded while those raised before are kept. When the database
    /// aborted the transaction because of a deadlock, the work then runs again from that state.
    /// Inside a transaction already open the work just runs.
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
