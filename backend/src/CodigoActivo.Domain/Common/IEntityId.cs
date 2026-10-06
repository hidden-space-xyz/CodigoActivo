namespace CodigoActivo.Domain.Common;

/// <summary>
/// Identifier of an entity of one type, so identifiers of different entities cannot be mixed up.
/// </summary>
/// <typeparam name="TSelf">The identifier type itself.</typeparam>
public interface IEntityId<TSelf>
    where TSelf : struct, IEntityId<TSelf>
{
    /// <summary>
    /// Gets the underlying identifier.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new identifier.
    /// </summary>
    /// <returns>A new identifier.</returns>
    public static abstract TSelf New();

    /// <summary>
    /// Wraps an existing identifier.
    /// </summary>
    /// <param name="value">Underlying identifier.</param>
    /// <returns>The identifier.</returns>
    public static abstract TSelf From(Guid value);
}
