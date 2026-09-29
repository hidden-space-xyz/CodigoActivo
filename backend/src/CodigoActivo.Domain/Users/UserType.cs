using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Membership type of an account, from the fixed catalog of types.
/// </summary>
public class UserType : NamedEntity
{
    private UserType() { }

    /// <summary>
    /// Gets the display color associated with the item.
    /// </summary>
    public string Color { get; private init; } = string.Empty;

    /// <summary>
    /// Creates an entry of the catalog.
    /// </summary>
    /// <param name="id">Known identifier of the type.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="description">Detailed description.</param>
    /// <param name="color">Display color.</param>
    /// <returns>The unsaved entry.</returns>
    public static UserType Create(Guid id, string name, string description, string color)
    {
        return new UserType
        {
            Id = id,
            Name = name,
            Description = description,
            Color = color,
        };
    }
}
