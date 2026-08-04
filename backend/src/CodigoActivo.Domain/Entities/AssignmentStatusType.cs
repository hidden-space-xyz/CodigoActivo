using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class AssignmentStatusType : NamedEntity
{
    public required string Color { get; set; }

    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];
}
