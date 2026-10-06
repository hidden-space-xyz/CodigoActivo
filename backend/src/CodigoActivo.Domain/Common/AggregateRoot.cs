namespace CodigoActivo.Domain.Common;

/// <summary>
/// Aggregate root with an identity of its own that records the domain events its behavior raises,
/// so they are published once its changes are committed.
/// </summary>
/// <typeparam name="TId">Identifier type of the aggregate.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot, IHasDomainEvents
    where TId : struct, IEntityId<TId>
{
    private readonly List<IDomainEvent> domainEvents = [];

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> PullDomainEvents()
    {
        var pulled = domainEvents.ToList();
        domainEvents.Clear();
        return pulled;
    }

    /// <summary>
    /// Records a domain event to publish after the commit.
    /// </summary>
    /// <param name="domainEvent">Event raised by the aggregate.</param>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        domainEvents.Add(domainEvent);
    }
}
