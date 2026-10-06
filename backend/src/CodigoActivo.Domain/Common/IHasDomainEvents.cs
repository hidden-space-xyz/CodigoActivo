namespace CodigoActivo.Domain.Common;

/// <summary>
/// Entity that records domain events until they are published.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>
    /// Returns the events recorded since the last call and forgets them.
    /// </summary>
    /// <returns>The recorded events, in the order they were raised.</returns>
    public IReadOnlyList<IDomainEvent> PullDomainEvents();
}
