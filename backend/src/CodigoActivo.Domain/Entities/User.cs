using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted user domain entity and its relationships.
/// </summary>
public class User : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the first name value.
    /// </summary>
    public required string FirstName { get; set; }
    /// <summary>
    /// Gets or sets the last name value.
    /// </summary>
    public required string LastName { get; set; }

    /// <summary>
    /// Gets or sets the email value.
    /// </summary>
    public string? Email { get; set; }
    /// <summary>
    /// Gets or sets the phone value.
    /// </summary>
    public string? Phone { get; set; }
    /// <summary>
    /// Gets or sets the password hash value.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Gets or sets the birth date value.
    /// </summary>
    public DateOnly BirthDate { get; set; }
    /// <summary>
    /// Gets or sets the associated gender.
    /// </summary>
    public Gender Gender { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the user's most recent login.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; set; }
    /// <summary>
    /// Gets or sets the UTC timestamp when the entity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent update.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated parent.
    /// </summary>
    public Guid? ParentId { get; set; }
    /// <summary>
    /// Gets or sets the associated parent.
    /// </summary>
    public User? Parent { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated user status type.
    /// </summary>
    public Guid UserStatusTypeId { get; set; }
    /// <summary>
    /// Gets or sets the associated user status type.
    /// </summary>
    public UserStatusType UserStatusType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the associated user type.
    /// </summary>
    public Guid UserTypeId { get; set; }
    /// <summary>
    /// Gets or sets the associated user type.
    /// </summary>
    public UserType UserType { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether admin.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Gets or sets the otp code hash value.
    /// </summary>
    public string? OtpCodeHash { get; set; }
    /// <summary>
    /// Gets or sets the otp expires at value.
    /// </summary>
    public DateTimeOffset? OtpExpiresAt { get; set; }
    /// <summary>
    /// Gets or sets the otp last sent at value.
    /// </summary>
    public DateTimeOffset? OtpLastSentAt { get; set; }

    /// <summary>
    /// Gets or sets the password reset code hash value.
    /// </summary>
    public string? PasswordResetCodeHash { get; set; }
    /// <summary>
    /// Gets or sets the password reset expires at value.
    /// </summary>
    public DateTimeOffset? PasswordResetExpiresAt { get; set; }
    /// <summary>
    /// Gets or sets the password reset last sent at value.
    /// </summary>
    public DateTimeOffset? PasswordResetLastSentAt { get; set; }

    /// <summary>
    /// Gets or sets the related children collection.
    /// </summary>
    public ICollection<User> Children { get; set; } = [];
    /// <summary>
    /// Gets or sets the related assignments collection.
    /// </summary>
    public ICollection<ActivityUserRoleAssignment> Assignments { get; set; } = [];

    /// <summary>
    /// Determines whether sue otp.
    /// </summary>
    /// <param name="codeHash">The code hash value.</param>
    /// <param name="now">Current timestamp used to calculate replenishment.</param>
    /// <param name="lifetime">The lifetime value.</param>
    public void IssueOtp(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        OtpCodeHash = codeHash;
        OtpExpiresAt = now + lifetime;
        OtpLastSentAt = now;
    }

    /// <summary>
    /// Clears the stored otp.
    /// </summary>
    public void ClearOtp()
    {
        OtpCodeHash = null;
        OtpExpiresAt = null;
        OtpLastSentAt = null;
    }

    /// <summary>
    /// Determines whether sue password reset code.
    /// </summary>
    /// <param name="codeHash">The code hash value.</param>
    /// <param name="now">Current timestamp used to calculate replenishment.</param>
    /// <param name="lifetime">The lifetime value.</param>
    public void IssuePasswordResetCode(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        PasswordResetCodeHash = codeHash;
        PasswordResetExpiresAt = now + lifetime;
        PasswordResetLastSentAt = now;
    }

    /// <summary>
    /// Clears the stored password reset code.
    /// </summary>
    public void ClearPasswordResetCode()
    {
        PasswordResetCodeHash = null;
        PasswordResetExpiresAt = null;
        PasswordResetLastSentAt = null;
    }

    /// <summary>
    /// Resets the password using the supplied value.
    /// </summary>
    /// <param name="passwordHash">The password hash value.</param>
    /// <param name="now">Current timestamp used to calculate replenishment.</param>
    public void ResetPassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        ClearPasswordResetCode();
        UpdatedAt = now;
    }

    /// <summary>
    /// Verifies the supplied value against its stored cryptographic representation.
    /// </summary>
    /// <param name="activeStatusId">Identifier of the active status.</param>
    /// <param name="now">Current timestamp used to calculate replenishment.</param>
    public void Verify(Guid activeStatusId, DateTimeOffset now)
    {
        UserStatusTypeId = activeStatusId;
        ClearOtp();
        UpdatedAt = now;
    }

    /// <summary>
    /// Records the login timestamp.
    /// </summary>
    /// <param name="now">Current timestamp used to calculate replenishment.</param>
    public void RegisterLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
    }
}
