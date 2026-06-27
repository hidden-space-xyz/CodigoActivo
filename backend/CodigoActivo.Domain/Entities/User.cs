using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class User : IdentifiableEntity
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }

    public DateOnly BirthDate { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? ParentId { get; set; }
    public User? Parent { get; set; }

    public Guid UserStatusTypeId { get; set; }
    public UserStatusType UserStatusType { get; set; } = null!;

    public Guid? OtpCode { get; set; }
    public DateTimeOffset? OtpExpiresAt { get; set; }

    public ICollection<UserTypeAssignment> TypeAssignments { get; set; } = [];
    public ICollection<User> Children { get; set; } = [];

    public void Verify(Guid activeStatusId)
    {
        UserStatusTypeId = activeStatusId;
        OtpCode = null;
        OtpExpiresAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RegisterLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
    }
}
