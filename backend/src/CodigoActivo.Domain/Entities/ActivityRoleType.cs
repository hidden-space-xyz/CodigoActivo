using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class ActivityRoleType : NamedEntity
{
    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];
}
