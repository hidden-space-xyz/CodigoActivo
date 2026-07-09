using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class AssignmentStatusType : NamedEntity
{
    public string Color { get; set; } = null!;

    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];
}
