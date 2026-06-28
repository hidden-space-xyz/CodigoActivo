using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class AssignmentStatusType : NamedEntity
{
    /// <summary>Hex color (e.g. <c>#22C55E</c>) used to render this status's tag in the UI.</summary>
    public string Color { get; set; } = null!;

    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];
}
