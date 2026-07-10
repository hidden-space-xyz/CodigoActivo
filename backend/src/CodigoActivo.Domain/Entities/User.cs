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

    public Guid UserTypeId { get; set; }
    public UserType UserType { get; set; } = null!;

    public bool IsAdmin { get; set; }

    public string? OtpCodeHash { get; set; }
    public DateTimeOffset? OtpExpiresAt { get; set; }
    public DateTimeOffset? OtpLastSentAt { get; set; }

    public ICollection<User> Children { get; set; } = [];

    public void IssueOtp(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        OtpCodeHash = codeHash;
        OtpExpiresAt = now + lifetime;
        OtpLastSentAt = now;
    }

    public void ClearOtp()
    {
        OtpCodeHash = null;
        OtpExpiresAt = null;
        OtpLastSentAt = null;
    }

    public void Verify(Guid activeStatusId, DateTimeOffset now)
    {
        UserStatusTypeId = activeStatusId;
        ClearOtp();
        UpdatedAt = now;
    }

    public void RegisterLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
    }
}
