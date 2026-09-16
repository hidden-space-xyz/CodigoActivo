using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted activity role type domain entity and its relationships.
/// </summary>
public class ActivityRoleType : NamedEntity
{
    /// <summary>
    /// Gets or sets the related assignments collection.
    /// </summary>
    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];
}
