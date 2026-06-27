namespace CodigoActivo.Domain.Entities;

public class UserTypeAssignment
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid UserTypeId { get; set; }
    public UserType UserType { get; set; } = null!;

    public DateTimeOffset AssignedAt { get; set; }
}
