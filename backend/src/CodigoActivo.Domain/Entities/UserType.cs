using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted user type domain entity and its relationships.
/// </summary>
public class UserType : NamedEntity
{
    /// <summary>
    /// Gets or sets the display color associated with the item.
    /// </summary>
    public required string Color { get; set; }

    /// <summary>
    /// Gets or sets the related users collection.
    /// </summary>
    public ICollection<User> Users { get; set; } = [];
}
