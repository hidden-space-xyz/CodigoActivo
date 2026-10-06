using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace CodigoActivo.Application.Common.Catalogs;

/// <summary>
/// Stable identifiers of the members of a closed catalog: the key of each row of its reference
/// table and the value its foreign keys and the API carry.
/// </summary>
/// <typeparam name="TValue">Domain enumeration of the catalog.</typeparam>
public sealed class CatalogKeys<TValue>
    where TValue : struct, Enum
{
    private readonly FrozenDictionary<TValue, Guid> ids;
    private readonly FrozenDictionary<Guid, TValue> values;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogKeys{TValue}"/> class.
    /// </summary>
    /// <param name="ids">Identifier of every member of the enumeration.</param>
    /// <exception cref="ArgumentException">A member has no identifier or two share one.</exception>
    public CatalogKeys(IReadOnlyDictionary<TValue, Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (
            Enum.GetValues<TValue>().Any(value => !ids.ContainsKey(value))
            || ids.Values.Distinct().Count() != ids.Count
        )
        {
            throw new ArgumentException("Every member needs its own identifier.", nameof(ids));
        }

        this.ids = ids.ToFrozenDictionary();
        values = ids.ToFrozenDictionary(pair => pair.Value, pair => pair.Key);
    }

    /// <summary>
    /// Gets the identifier of every member.
    /// </summary>
    public IReadOnlyDictionary<TValue, Guid> Ids => ids;

    /// <summary>
    /// Gets the identifier of a member.
    /// </summary>
    /// <param name="value">Member of the catalog.</param>
    /// <returns>Its stable identifier.</returns>
    public Guid IdOf(TValue value)
    {
        return ids[value];
    }

    /// <summary>
    /// Gets the member an identifier names.
    /// </summary>
    /// <param name="id">Identifier read from storage.</param>
    /// <returns>The member.</returns>
    /// <exception cref="KeyNotFoundException">No member has that identifier.</exception>
    public TValue ValueOf(Guid id)
    {
        return values[id];
    }

    /// <summary>
    /// Looks up the member an identifier names.
    /// </summary>
    /// <param name="id">Identifier supplied by a client.</param>
    /// <param name="value">The member, when one has that identifier.</param>
    /// <returns><see langword="true"/> when a member has that identifier.</returns>
    public bool TryGetValue(Guid id, [MaybeNullWhen(false)] out TValue value)
    {
        return values.TryGetValue(id, out value);
    }
}
