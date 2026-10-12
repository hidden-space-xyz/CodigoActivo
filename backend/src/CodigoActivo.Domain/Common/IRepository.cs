namespace CodigoActivo.Domain.Common;

/// <summary>
/// Collection-like access to the stored instances of an aggregate. Each repository adds the
/// intention-revealing lookups its use cases need; none exposes query composition. Adding and
/// removing only stage changes for the unit of work; the few methods that write immediately,
/// without waiting for the unit of work, say so by their verb (<c>Save</c>, <c>End</c>,
/// <c>Purge</c>).
/// </summary>
/// <typeparam name="TAggregate">Root of the aggregate the repository stores.</typeparam>
public interface IRepository<in TAggregate>
    where TAggregate : class, IAggregateRoot
{
    /// <summary>
    /// Adds a new aggregate, stored when the unit of work commits.
    /// </summary>
    /// <param name="aggregate">Aggregate to add.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that completes when the aggregate is tracked.</returns>
    public Task AddAsync(TAggregate aggregate, CancellationToken ct = default);

    /// <summary>
    /// Removes an aggregate, deleted when the unit of work commits.
    /// </summary>
    /// <param name="aggregate">Aggregate to remove.</param>
    public void Remove(TAggregate aggregate);
}
