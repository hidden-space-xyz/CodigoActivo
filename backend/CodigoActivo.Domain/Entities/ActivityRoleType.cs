using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class ActivityRoleType : NamedEntity
{
    public ICollection<ActivityAllowedRoleType> AllowedInActivities { get; set; } = [];
    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];
}
