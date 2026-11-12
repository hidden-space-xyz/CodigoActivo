using System.Text;
using System.Text.Json;

namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// Values an inventory entry holds, each written in the canonical form of its column type and
/// keyed by the column. A column without a value is absent; the quantity is kept apart by
/// <see cref="InventoryEntry.Quantity"/>. Two sets are equal when they hold the same values.
/// </summary>
public sealed class InventoryValues : IEquatable<InventoryValues>
{
    private readonly SortedDictionary<Guid, string> values;

    private InventoryValues(SortedDictionary<Guid, string> values)
    {
        this.values = values;
        Json = Write(values);
    }

    /// <summary>
    /// Gets the set without any value.
    /// </summary>
    public static InventoryValues Empty { get; } = new([]);

    /// <summary>
    /// Gets the JSON object the values are stored as: column identifiers as keys, in order, and
    /// the values as strings.
    /// </summary>
    public string Json { get; }

    /// <summary>
    /// Gets each value by the identifier of its column.
    /// </summary>
    public IReadOnlyDictionary<InventoryColumnId, string> ByColumn =>
        values.ToDictionary(pair => InventoryColumnId.From(pair.Key), pair => pair.Value);

    /// <summary>
    /// Reads the values stored as <see cref="Json"/>.
    /// </summary>
    /// <param name="json">Stored JSON object of string values.</param>
    /// <returns>The values.</returns>
    public static InventoryValues FromStored(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        var stored = new SortedDictionary<Guid, string>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            stored[Guid.Parse(property.Name)] = property.Value.GetString() ?? string.Empty;
        }

        return new InventoryValues(stored);
    }

    /// <summary>
    /// Gets the value of a column.
    /// </summary>
    /// <param name="column">Identifier of the column.</param>
    /// <returns>The value, or <see langword="null"/> when the column has none.</returns>
    public string? Of(InventoryColumnId column)
    {
        return values.GetValueOrDefault(column.Value);
    }

    /// <inheritdoc />
    public bool Equals(InventoryValues? other)
    {
        return other is not null && string.Equals(Json, other.Json, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as InventoryValues);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Json.GetHashCode(StringComparison.Ordinal);
    }

    internal static InventoryValues Of(IEnumerable<KeyValuePair<InventoryColumnId, string>> pairs)
    {
        var read = new SortedDictionary<Guid, string>();
        foreach (var (column, value) in pairs)
        {
            read[column.Value] = value;
        }

        return new InventoryValues(read);
    }

    internal InventoryValues Without(InventoryColumnId column)
    {
        if (!values.ContainsKey(column.Value))
        {
            return this;
        }

        var kept = new SortedDictionary<Guid, string>(values);
        kept.Remove(column.Value);
        return new InventoryValues(kept);
    }

    private static string Write(SortedDictionary<Guid, string> values)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (column, value) in values)
            {
                writer.WriteString(column.ToString(), value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
