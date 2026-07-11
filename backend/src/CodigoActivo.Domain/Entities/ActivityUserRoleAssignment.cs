namespace CodigoActivo.Domain.Entities;

public class ActivityUserRoleAssignment
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public Guid ActivityRoleTypeId { get; set; }
    public ActivityRoleType ActivityRoleType { get; set; } = null!;

    public Guid AssignmentStatusId { get; set; }
    public AssignmentStatusType AssignmentStatus { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
