using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class AssignmentStatusType : NamedEntity
{
    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];
}
