namespace CodigoActivo.Domain.Common;

/// <summary>
/// Entity with an identity of its own, fixed when it is created.
/// </summary>
/// <typeparam name="TId">Identifier type of the entity.</typeparam>
public abstract class Entity<TId>
    where TId : struct, IEntityId<TId>
{
    /// <summary>
    /// Gets the unique identifier, fixed when the entity is created.
    /// </summary>
    public TId Id { get; protected init; } = TId.New();
}
