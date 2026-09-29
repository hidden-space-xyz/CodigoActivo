using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Resources;

/// <summary>
/// Kind of resource, from the fixed catalog of kinds: it decides whether a resource is content of
/// the site or a link to an external page.
/// </summary>
public class ResourceType : NamedEntity
{
    private ResourceType() { }

    /// <summary>
    /// Gets the display color associated with the item.
    /// </summary>
    public string Color { get; private init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether resources of this kind are links to external pages.
    /// </summary>
    public bool IsExternal { get; private init; }

    /// <summary>
    /// Creates an entry of the catalog.
    /// </summary>
    /// <param name="id">Known identifier of the kind.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="description">Detailed description.</param>
    /// <param name="color">Display color.</param>
    /// <param name="isExternal">Whether resources of this kind are links to external pages.</param>
    /// <returns>The unsaved entry.</returns>
    public static ResourceType Create(
        Guid id,
        string name,
        string description,
        string color,
        bool isExternal
    )
    {
        return new ResourceType
        {
            Id = id,
            Name = name,
            Description = description,
            Color = color,
            IsExternal = isExternal,
        };
    }
}
