using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Status of a signup, from the fixed catalog of statuses.
/// </summary>
public class AssignmentStatusType : NamedEntity
{
    private AssignmentStatusType() { }

    /// <summary>
    /// Gets the display color associated with the item.
    /// </summary>
    public string Color { get; private init; } = string.Empty;

    /// <summary>
    /// Creates an entry of the catalog.
    /// </summary>
    /// <param name="id">Known identifier of the status.</param>
    /// <param name="name">Human-readable name.</param>
    /// <param name="description">Detailed description.</param>
    /// <param name="color">Display color.</param>
    /// <returns>The unsaved entry.</returns>
    public static AssignmentStatusType Create(
        Guid id,
        string name,
        string description,
        string color
    )
    {
        return new AssignmentStatusType
        {
            Id = id,
            Name = name,
            Description = description,
            Color = color,
        };
    }
}
