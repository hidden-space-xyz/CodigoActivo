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
    /// Gets or sets the birth date value. Only dependents have one; independent accounts leave it
    /// unset.
    /// </summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>
    /// Gets or sets the normalized Spanish national identity number (DNI or NIE): uppercase, without
    /// spaces or hyphens. Required and unique for independent accounts; unset for dependents.
    /// </summary>
    public string? NationalId { get; set; }

    /// <summary>
    /// Gets or sets whether the user agreed to receive promotional content. Always
    /// <see langword="false"/> for dependents.
    /// </summary>
    public bool PromotionalConsent { get; set; }

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
    /// Gets or sets the second factor required after the password. Every account has one.
    /// </summary>
    public TwoFactorMethod TwoFactorMethod { get; set; } = TwoFactorMethod.Email;

    /// <summary>
    /// Gets or sets the protected shared secret of the active authenticator application.
    /// </summary>
    public string? AuthenticatorKey { get; set; }

    /// <summary>
    /// Gets or sets the last authenticator time step accepted, so a code cannot be replayed.
    /// </summary>
    public long? AuthenticatorLastUsedStep { get; set; }

    /// <summary>
    /// Gets or sets the protected shared secret of an authenticator being enrolled.
    /// </summary>
    public string? PendingAuthenticatorKey { get; set; }

    /// <summary>
    /// Gets or sets when the pending authenticator enrollment stops being confirmable.
    /// </summary>
    public DateTimeOffset? PendingAuthenticatorExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the hash of the emailed login code of the open two-factor challenge.
    /// </summary>
    public string? LoginCodeHash { get; set; }

    /// <summary>
    /// Gets or sets when the emailed login code expires.
    /// </summary>
    public DateTimeOffset? LoginCodeExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when the emailed login code was last sent.
    /// </summary>
    public DateTimeOffset? LoginCodeLastSentAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the open second-factor challenge. The challenge cookie
    /// carries it, so a copied cookie stops working as soon as this value changes or is cleared.
    /// </summary>
    public Guid? LoginChallengeId { get; set; }

    /// <summary>
    /// Gets or sets the consecutive wrong second-factor codes since the last success or lockout.
    /// </summary>
    public int TwoFactorFailedAttempts { get; set; }

    /// <summary>
    /// Gets or sets until when second-factor codes are rejected after too many failures.
    /// </summary>
    public DateTimeOffset? TwoFactorLockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the consecutive wrong account passwords since the last accepted one.
    /// </summary>
    public int PasswordFailedAttempts { get; set; }

    /// <summary>
    /// Gets or sets when the account was locked after too many wrong passwords. The lock does not
    /// expire: only a completed password reset clears it.
    /// </summary>
    public DateTimeOffset? PasswordLockedAt { get; set; }

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
        ClearPasswordFailures();
        ClearLoginChallenge();
        PasswordLockedAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Determines whether the account is locked after too many wrong passwords. A locked account
    /// refuses every password, correct or not, until its password is reset through recovery.
    /// </summary>
    /// <returns><see langword="true"/> when passwords must be rejected regardless of their value.</returns>
    public bool IsPasswordLocked()
    {
        return PasswordLockedAt is not null;
    }

    /// <summary>
    /// Forgets the consecutive wrong passwords counted so far. Counting a wrong password and
    /// locking the account happen in one atomic statement in
    /// <see cref="Repositories.IUserRepository.RecordPasswordFailureAsync"/>, never in memory, so
    /// parallel attempts cannot lose each other's increments.
    /// </summary>
    public void ClearPasswordFailures()
    {
        PasswordFailedAttempts = 0;
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

    /// <summary>
    /// Stores a new emailed login code, replacing any previous one.
    /// </summary>
    /// <param name="codeHash">Hash of the code that was emailed.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="lifetime">How long the code stays valid.</param>
    public void IssueLoginCode(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        LoginCodeHash = codeHash;
        LoginCodeExpiresAt = now + lifetime;
        LoginCodeLastSentAt = now;
    }

    /// <summary>
    /// Clears the emailed login code and its timestamps.
    /// </summary>
    public void ClearLoginCode()
    {
        LoginCodeHash = null;
        LoginCodeExpiresAt = null;
        LoginCodeLastSentAt = null;
    }

    /// <summary>
    /// Opens a second-factor challenge under a new identifier, replacing any open one so the
    /// cookie of an earlier password step stops being accepted.
    /// </summary>
    /// <param name="challengeId">Identifier the challenge cookie will carry.</param>
    public void StartLoginChallenge(Guid challengeId)
    {
        LoginChallengeId = challengeId;
    }

    /// <summary>
    /// Closes the open second-factor challenge, if any, so its cookie is refused from now on.
    /// </summary>
    public void ClearLoginChallenge()
    {
        LoginChallengeId = null;
    }

    /// <summary>
    /// Determines whether an emailed login code exists that is still valid and was sent recently
    /// enough that a new one should not be issued yet.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <param name="resendCooldown">Minimum time between two emailed codes.</param>
    /// <returns><see langword="true"/> when the existing code can still be used.</returns>
    public bool HasRecentLoginCode(DateTimeOffset now, TimeSpan resendCooldown)
    {
        return LoginCodeHash is not null
            && LoginCodeExpiresAt > now
            && LoginCodeLastSentAt + resendCooldown > now;
    }

    /// <summary>
    /// Determines whether second-factor verification is temporarily locked.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <returns><see langword="true"/> when codes must be rejected regardless of their value.</returns>
    public bool IsTwoFactorLocked(DateTimeOffset now)
    {
        return TwoFactorLockedUntil > now;
    }

    /// <summary>
    /// Counts a wrong second-factor code and locks verification once the limit is reached.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <param name="maxFailedAttempts">Failures allowed before locking.</param>
    /// <param name="lockoutDuration">How long the lock lasts.</param>
    /// <returns><see langword="true"/> when this failure triggered the lock.</returns>
    public bool RecordTwoFactorFailure(
        DateTimeOffset now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration
    )
    {
        TwoFactorFailedAttempts++;
        if (TwoFactorFailedAttempts < maxFailedAttempts)
        {
            return false;
        }

        TwoFactorFailedAttempts = 0;
        TwoFactorLockedUntil = now + lockoutDuration;
        ClearLoginCode();
        ClearLoginChallenge();
        return true;
    }

    /// <summary>
    /// Completes a login after the second factor was accepted.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    public void CompleteTwoFactorLogin(DateTimeOffset now)
    {
        ClearLoginCode();
        ClearLoginChallenge();
        TwoFactorFailedAttempts = 0;
        TwoFactorLockedUntil = null;
        RegisterLogin(now);
    }

    /// <summary>
    /// Stores the protected secret of an authenticator that still has to be confirmed.
    /// </summary>
    /// <param name="protectedKey">Protected shared secret.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="lifetime">How long the enrollment can be confirmed.</param>
    public void BeginAuthenticatorSetup(string protectedKey, DateTimeOffset now, TimeSpan lifetime)
    {
        PendingAuthenticatorKey = protectedKey;
        PendingAuthenticatorExpiresAt = now + lifetime;
    }

    /// <summary>
    /// Determines whether an authenticator enrollment is waiting for confirmation.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <returns><see langword="true"/> when a pending key exists and has not expired.</returns>
    public bool HasPendingAuthenticator(DateTimeOffset now)
    {
        return PendingAuthenticatorKey is not null && PendingAuthenticatorExpiresAt > now;
    }

    /// <summary>
    /// Promotes the pending authenticator to the active second factor.
    /// </summary>
    /// <param name="usedStep">Time step of the code that confirmed the enrollment.</param>
    /// <param name="now">Current timestamp.</param>
    public void EnableAuthenticator(long usedStep, DateTimeOffset now)
    {
        AuthenticatorKey = PendingAuthenticatorKey;
        AuthenticatorLastUsedStep = usedStep;
        PendingAuthenticatorKey = null;
        PendingAuthenticatorExpiresAt = null;
        TwoFactorMethod = TwoFactorMethod.Authenticator;
        ClearLoginCode();
        UpdatedAt = now;
    }

    /// <summary>
    /// Makes email the second factor again and forgets any authenticator, active or pending.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    public void UseEmailTwoFactor(DateTimeOffset now)
    {
        TwoFactorMethod = TwoFactorMethod.Email;
        AuthenticatorKey = null;
        AuthenticatorLastUsedStep = null;
        PendingAuthenticatorKey = null;
        PendingAuthenticatorExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Returns the second factor to its default state: email, no authenticator, no lock and no
    /// open challenge. Used by administrators when a user loses access to their authenticator.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    public void ResetTwoFactor(DateTimeOffset now)
    {
        UseEmailTwoFactor(now);
        ClearLoginCode();
        TwoFactorFailedAttempts = 0;
        TwoFactorLockedUntil = null;
    }
}
