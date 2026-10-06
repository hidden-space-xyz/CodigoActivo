namespace CodigoActivo.Infrastructure.Database.Catalogs;

/// <summary>
/// Row of the reference table of a closed catalog: the name shown to people for one member of
/// its domain enumeration.
/// </summary>
/// <typeparam name="TValue">Domain enumeration of the catalog.</typeparam>
public abstract class CatalogEntry<TValue>
    where TValue : struct, Enum
{
    /// <summary>
    /// Gets the member the row describes, stored as its stable identifier.
    /// </summary>
    public TValue Id { get; init; }

    /// <summary>
    /// Gets the name shown to people.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
