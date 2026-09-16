using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted assignment status type domain entity and its relationships.
/// </summary>
public class AssignmentStatusType : NamedEntity
{
    /// <summary>
    /// Gets or sets the display color associated with the item.
    /// </summary>
    public required string Color { get; set; }

    /// <summary>
    /// Gets or sets the related assignments collection.
    /// </summary>
    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];
}
