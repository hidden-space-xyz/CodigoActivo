namespace CodigoActivo.Domain.Common;

/// <summary>
/// Entry of a fixed catalog: reference data seeded with a known identifier, a name and a
/// description, that no use case changes.
/// </summary>
public abstract class NamedEntity : IdentifiableEntity
{
    /// <summary>
    /// Gets the human-readable name.
    /// </summary>
    public string Name { get; protected init; } = string.Empty;

    /// <summary>
    /// Gets the detailed description.
    /// </summary>
    public string Description { get; protected init; } = string.Empty;
}
