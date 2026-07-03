namespace CodigoActivo.Domain.Entities;

public class ActivityAllowedRoleType
{
    public Guid ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public Guid ActivityRoleTypeId { get; set; }
    public ActivityRoleType ActivityRoleType { get; set; } = null!;
}