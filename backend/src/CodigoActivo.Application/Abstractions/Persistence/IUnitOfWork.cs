namespace CodigoActivo.Application.Abstractions.Persistence;

/// <summary>
/// Commits the changes staged by a use case as one unit.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Commits every staged change and then publishes the domain events it raised, or, inside a
    /// transaction of <see cref="ExecuteInTransactionAsync{T}"/>, once that transaction commits.
    /// When the database refuses the commit, the changes stay staged with the domain events they
    /// raised, so a later commit that includes them publishes them.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of rows written.</returns>
    /// <exception cref="UniqueConstraintViolationException">
    /// A unique rule of the database rejected the commit.
    /// </exception>
    /// <exception cref="ConcurrentUpdateException">
    /// A concurrent transaction wrote or removed a row of the commit after it was read.
    /// </exception>
    public Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Forgets every change staged since the last commit, with the domain events it raised, so a
    /// use case that gives up after the database refused a commit leaves nothing for a later one.
    /// </summary>
    public void DiscardChanges();

    /// <summary>
    /// Runs work inside a database transaction and commits it when the work returns, whatever
    /// result it returns, so a refusal keeps what the work saved, such as a counted wrong code;
    /// changes the work staged without saving them stay staged. When the work throws, the
    /// transaction rolls back, the domain events of what the work saved are discarded and the
    /// exception propagates. What the work saved still counts as stored for the unit of work,
    /// so a use case that carries on after the failure calls <see cref="DiscardChanges"/> first.
    /// Inside a transaction already open the work just runs.
    /// </summary>
    /// <typeparam name="T">Type of the result of the work.</typeparam>
    /// <param name="work">Work to run; it receives the cancellation token.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the result of the work.</returns>
    /// <exception cref="ConcurrentUpdateException">
    /// The database aborted the transaction to resolve a deadlock with a concurrent one.
    /// </exception>
    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken ct = default
    );
}
