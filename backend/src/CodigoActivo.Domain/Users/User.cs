using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Represents the persisted user domain entity and its relationships. A person is either an
/// independent account, which logs in and needs an email, a phone and a DNI or NIE, or a dependent
/// of a guardian, which has none of those and needs a birth date instead. Create them with
/// <see cref="CreateIndependent"/> and <see cref="CreateDependent"/>, and edit them with
/// <see cref="PlanProfileChange"/> and <see cref="ApplyProfileChange"/>, which own those rules.
/// </summary>
public class User : IdentifiableEntity, IAggregateRoot
{
    private User() { }

    /// <summary>
    /// Gets the first name value.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the last name value.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the email value.
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>
    /// Gets the phone value.
    /// </summary>
    public string? Phone { get; private set; }

    /// <summary>
    /// Gets the optional second contact phone of an independent account. It is never
    /// equal to <see cref="Phone"/> and is always unset for dependents.
    /// </summary>
    public string? SecondaryPhone { get; private set; }

    /// <summary>
    /// Gets the password hash value.
    /// </summary>
    public string? PasswordHash { get; private set; }

    /// <summary>
    /// Gets the birth date value. Only dependents have one; independent accounts leave it
    /// unset.
    /// </summary>
    public DateOnly? BirthDate { get; private set; }

    /// <summary>
    /// Gets the normalized Spanish national identity number (DNI or NIE): uppercase, without
    /// spaces or hyphens. Required for independent accounts but not unique; unset for dependents.
    /// </summary>
    public string? NationalId { get; private set; }

    /// <summary>
    /// Gets whether the user agreed to receive promotional content. Always
    /// <see langword="false"/> for dependents.
    /// </summary>
    public bool PromotionalConsent { get; private set; }

    /// <summary>
    /// Gets the associated gender.
    /// </summary>
    public Gender Gender { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp of the user's most recent login.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the entity was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp of the most recent update.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the associated parent.
    /// </summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// Gets the identifier of the associated user status type.
    /// </summary>
    public Guid UserStatusTypeId { get; private set; }

    /// <summary>
    /// Gets the identifier of the associated user type.
    /// </summary>
    public Guid UserTypeId { get; private set; }

    /// <summary>
    /// Gets whether admin.
    /// </summary>
    public bool IsAdmin { get; private set; }

    /// <summary>
    /// Gets the otp code hash value.
    /// </summary>
    public string? OtpCodeHash { get; private set; }

    /// <summary>
    /// Gets the otp expires at value.
    /// </summary>
    public DateTimeOffset? OtpExpiresAt { get; private set; }

    /// <summary>
    /// Gets the otp last sent at value.
    /// </summary>
    public DateTimeOffset? OtpLastSentAt { get; private set; }

    /// <summary>
    /// Gets the password reset code hash value.
    /// </summary>
    public string? PasswordResetCodeHash { get; private set; }

    /// <summary>
    /// Gets the password reset expires at value.
    /// </summary>
    public DateTimeOffset? PasswordResetExpiresAt { get; private set; }

    /// <summary>
    /// Gets the password reset last sent at value.
    /// </summary>
    public DateTimeOffset? PasswordResetLastSentAt { get; private set; }

    /// <summary>
    /// Gets the second factor required after the password. Every account has one.
    /// </summary>
    public TwoFactorMethod TwoFactorMethod { get; private set; } = TwoFactorMethod.Email;

    /// <summary>
    /// Gets the protected shared secret of the active authenticator application.
    /// </summary>
    public string? AuthenticatorKey { get; private set; }

    /// <summary>
    /// Gets the last authenticator time step accepted, so a code cannot be replayed.
    /// </summary>
    public long? AuthenticatorLastUsedStep { get; private set; }

    /// <summary>
    /// Gets the protected shared secret of an authenticator being enrolled.
    /// </summary>
    public string? PendingAuthenticatorKey { get; private set; }

    /// <summary>
    /// Gets when the pending authenticator enrollment stops being confirmable.
    /// </summary>
    public DateTimeOffset? PendingAuthenticatorExpiresAt { get; private set; }

    /// <summary>
    /// Gets the hash of the emailed login code of the open two-factor challenge.
    /// </summary>
    public string? LoginCodeHash { get; private set; }

    /// <summary>
    /// Gets when the emailed login code expires.
    /// </summary>
    public DateTimeOffset? LoginCodeExpiresAt { get; private set; }

    /// <summary>
    /// Gets when the emailed login code was last sent.
    /// </summary>
    public DateTimeOffset? LoginCodeLastSentAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the open second-factor challenge. The challenge cookie
    /// carries it, so a copied cookie stops working as soon as this value changes or is cleared.
    /// </summary>
    public Guid? LoginChallengeId { get; private set; }

    /// <summary>
    /// Gets the consecutive wrong second-factor codes since the last success or lockout.
    /// </summary>
    public int TwoFactorFailedAttempts { get; private set; }

    /// <summary>
    /// Gets until when second-factor codes are rejected after too many failures.
    /// </summary>
    public DateTimeOffset? TwoFactorLockedUntil { get; private set; }

    /// <summary>
    /// Gets the consecutive wrong account passwords since the last accepted one.
    /// </summary>
    public int PasswordFailedAttempts { get; private set; }

    /// <summary>
    /// Gets when the account was locked after too many wrong passwords. The lock does not
    /// expire: only a completed password reset clears it.
    /// </summary>
    public DateTimeOffset? PasswordLockedAt { get; private set; }

    /// <summary>
    /// Creates an independent account, pending email verification and as a participant. It needs
    /// a valid DNI or NIE, an email and a phone, may add a secondary phone different from the
    /// phone, and has no birth date. The caller sets the password hash and the verification code
    /// once the checks that need I/O have passed.
    /// </summary>
    /// <param name="details">Details as supplied by the registrant.</param>
    /// <param name="now">Current timestamp, stored as the creation time.</param>
    /// <param name="id">Stable identifier for provisioned accounts; a new one otherwise.</param>
    /// <returns>The unsaved account, or the error of the first broken rule.</returns>
    public static Result<User> CreateIndependent(
        PersonDetails details,
        DateTimeOffset now,
        Guid? id = null
    )
    {
        ArgumentNullException.ThrowIfNull(details);
        var account = new User
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = string.Empty,
            LastName = string.Empty,
            UserStatusTypeId = SeedIds.UserStatusTypes.Pending,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = now,
        };
        var change = account.PlanIndependentChange(details, guardianId: null);
        if (change.IsFailure)
        {
            return change.Error!;
        }

        account.Apply(change.Value);
        return account;
    }

    /// <summary>
    /// Creates a dependent of <paramref name="guardian"/> as a participant. It needs a birth date
    /// that is not in the future and makes it a minor on <paramref name="today"/>, and it has no
    /// email, phones, DNI, NIE, password or promotional consent. A dependent cannot be a guardian.
    /// </summary>
    /// <param name="guardian">Independent account the dependent is created under.</param>
    /// <param name="details">Details as supplied; only the names, gender and birth date apply.</param>
    /// <param name="today">Local day on which the age is evaluated.</param>
    /// <param name="now">Current timestamp, stored as the creation time.</param>
    /// <param name="id">Stable identifier for provisioned accounts; a new one otherwise.</param>
    /// <returns>The unsaved dependent, or the error of the first broken rule.</returns>
    public static Result<User> CreateDependent(
        User guardian,
        PersonDetails details,
        DateOnly today,
        DateTimeOffset now,
        Guid? id = null
    )
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(details);
        if (guardian.ParentId is not null)
        {
            return Error.Validation(ErrorCode.UserParentIsMinor);
        }

        var dependent = new User
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = string.Empty,
            LastName = string.Empty,
            ParentId = guardian.Id,
            UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = now,
        };
        var change = dependent.PlanDependentChange(details, guardianId: null, today);
        if (change.IsFailure)
        {
            return change.Error!;
        }

        dependent.Apply(change.Value);
        return dependent;
    }

    /// <summary>
    /// Checks and normalizes a change to the profile without applying it. The stored account
    /// decides which rules apply, never the details: an independent account needs a valid DNI or
    /// NIE, an email and a phone, may add a secondary phone different from the phone, and is
    /// refused a birth date or a guardian; a dependent keeps its guardian, needs a birth date and
    /// ignores the contact details, DNI, NIE and consent. A dependent's birth date must keep it a
    /// minor only when it changes, so a dependent that has come of age stays editable.
    /// </summary>
    /// <param name="details">Details as supplied.</param>
    /// <param name="guardianId">
    /// Guardian named by the caller; only accepted for a dependent, repeating the one it has.
    /// </param>
    /// <param name="today">Local day on which a dependent's age is evaluated.</param>
    /// <returns>The planned change, or the error of the first broken rule.</returns>
    public Result<ProfileChange> PlanProfileChange(
        PersonDetails details,
        Guid? guardianId,
        DateOnly today
    )
    {
        ArgumentNullException.ThrowIfNull(details);
        return ParentId is null
            ? PlanIndependentChange(details, guardianId)
            : PlanDependentChange(details, guardianId, today);
    }

    /// <summary>
    /// Applies a change planned for this account by <see cref="PlanProfileChange"/>.
    /// </summary>
    /// <param name="change">Change planned for this account.</param>
    /// <param name="now">Current timestamp, stored as the update time.</param>
    /// <exception cref="ArgumentException">The change was planned for another account.</exception>
    public void ApplyProfileChange(ProfileChange change, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!ReferenceEquals(change.Account, this))
        {
            throw new ArgumentException(
                "The profile change was planned for another account.",
                nameof(change)
            );
        }

        Apply(change);
        UpdatedAt = now;
    }

    private Result<ProfileChange> PlanIndependentChange(PersonDetails details, Guid? guardianId)
    {
        if (details.BirthDate is not null)
        {
            return Error.Validation(ErrorCode.UserBirthDateNotAllowedForAdult);
        }

        if (guardianId is not null)
        {
            return Error.Validation(ErrorCode.UserParentNotAllowedForAdult);
        }

        var nationalId = SpanishNationalId.Normalize(details.NationalId);
        if (nationalId is null)
        {
            return Error.Validation(ErrorCode.UserNationalIdRequired);
        }

        if (!SpanishNationalId.IsValid(nationalId))
        {
            return Error.Validation(ErrorCode.RequestValidationFailed);
        }

        var contact = ContactDetails.From(details);
        if (contact.IsFailure)
        {
            return contact.Error!;
        }

        return new ProfileChange(this, details, contact.Value, nationalId, birthDate: null);
    }

    private Result<ProfileChange> PlanDependentChange(
        PersonDetails details,
        Guid? guardianId,
        DateOnly today
    )
    {
        if (guardianId is { } requested && requested != ParentId)
        {
            return Error.Forbidden(ErrorCode.UserParentReassignmentForbidden);
        }

        if (details.BirthDate is not { } birthDate)
        {
            return Error.Validation(ErrorCode.UserChildBirthDateRequired);
        }

        if (birthDate != BirthDate)
        {
            if (birthDate > today)
            {
                return Error.Validation(ErrorCode.RequestValidationFailed);
            }

            if (!birthDate.IsMinor(today))
            {
                return Error.Validation(ErrorCode.UserChildBirthDateNotMinor);
            }
        }

        return new ProfileChange(this, details, contact: null, nationalId: null, birthDate);
    }

    private void Apply(ProfileChange change)
    {
        FirstName = change.FirstName;
        LastName = change.LastName;
        Gender = change.Gender;
        NationalId = change.NationalId;
        PromotionalConsent = change.PromotionalConsent;
        if (change.Contact is { } contact)
        {
            Email = contact.Email;
            Phone = contact.Phone;
            SecondaryPhone = contact.SecondaryPhone;
            return;
        }

        BirthDate = change.BirthDate;
    }

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
    /// Tells whether the account is a dependent in the care of a guardian.
    /// </summary>
    /// <param name="guardianId">Identifier of the guardian.</param>
    /// <returns><see langword="true"/> when the guardian is responsible for this account.</returns>
    public bool IsDependentOf(Guid guardianId)
    {
        return ParentId == guardianId;
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
    /// Forgets the consecutive wrong passwords counted so far. Wrong passwords are counted by
    /// <see cref="RecordPasswordFailure"/> while the account row is locked, so parallel attempts
    /// cannot lose each other's increments.
    /// </summary>
    public void ClearPasswordFailures()
    {
        PasswordFailedAttempts = 0;
    }

    /// <summary>
    /// Activates the account once its email is verified, and forgets the verification code.
    /// </summary>
    /// <param name="now">Current time.</param>
    public void Verify(DateTimeOffset now)
    {
        UserStatusTypeId = SeedIds.UserStatusTypes.Active;
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
    /// open challenge. Used by administrators when a user loses access to their authenticator or
    /// locked their second factor.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <returns><see langword="true"/> when the account used an authenticator and now uses email.</returns>
    public bool ResetTwoFactor(DateTimeOffset now)
    {
        var leftAuthenticator = TwoFactorMethod is not TwoFactorMethod.Email;
        UseEmailTwoFactor(now);
        ClearLoginCode();
        TwoFactorFailedAttempts = 0;
        TwoFactorLockedUntil = null;
        return leftAuthenticator;
    }

    /// <summary>
    /// Creates the initial administrator: an active member with administrator rights and a
    /// placeholder DNI, the account that keeps the application administered.
    /// </summary>
    /// <param name="email">Normalized email address.</param>
    /// <param name="passwordHash">Hash of the password.</param>
    /// <param name="now">Current timestamp, stored as the creation time.</param>
    /// <returns>The unsaved account.</returns>
    public static User CreateInitialAdministrator(
        string email,
        string passwordHash,
        DateTimeOffset now
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Id = InitialAdministrator.Id,
            FirstName = "Administrador",
            LastName = "Código Activo",
            Email = email,
            PasswordHash = passwordHash,
            NationalId = SpanishNationalId.FromDniNumber(0),
            Gender = Gender.Other,
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Member,
            IsAdmin = true,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Gets a value indicating whether the account can open sessions: it is active and has a
    /// password.
    /// </summary>
    public bool CanSignIn =>
        UserStatusTypeId == SeedIds.UserStatusTypes.Active && PasswordHash is not null;

    /// <summary>
    /// Gets a value indicating whether an administrator blocked the account.
    /// </summary>
    public bool IsBlocked => UserStatusTypeId == SeedIds.UserStatusTypes.Blocked;

    /// <summary>
    /// Gets a value indicating whether the account is a dependent, which never signs in by itself.
    /// </summary>
    public bool IsDependent => UserStatusTypeId == SeedIds.UserStatusTypes.Dependent;

    /// <summary>
    /// Gets a value indicating whether the account still waits for the verification of its email.
    /// </summary>
    public bool IsPendingVerification => UserStatusTypeId == SeedIds.UserStatusTypes.Pending;

    /// <summary>
    /// Gets the hash of the verification code while the code can still be used.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The hash, or <see langword="null"/> when there is no code or it expired.</returns>
    public string? UsableOtpCodeHash(DateTimeOffset now)
    {
        return OtpExpiresAt >= now ? OtpCodeHash : null;
    }

    /// <summary>
    /// Gets the hash of the emailed login code while the code can still be used.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The hash, or <see langword="null"/> when there is no code or it expired.</returns>
    public string? UsableLoginCodeHash(DateTimeOffset now)
    {
        return LoginCodeExpiresAt >= now ? LoginCodeHash : null;
    }

    /// <summary>
    /// Gets the hash of the password reset code while the code can still be used.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The hash, or <see langword="null"/> when there is no code or it expired.</returns>
    public string? UsablePasswordResetCodeHash(DateTimeOffset now)
    {
        return PasswordResetExpiresAt >= now ? PasswordResetCodeHash : null;
    }

    /// <summary>
    /// Tells whether a code of the authenticator may be accepted. A code of the time step already
    /// accepted, or of an earlier one, is refused, so a code cannot be used twice.
    /// </summary>
    /// <param name="step">Time step of the code.</param>
    /// <returns><see langword="true"/> when the step is newer than the last one accepted.</returns>
    public bool AcceptsAuthenticatorStep(long step)
    {
        return AuthenticatorLastUsedStep is not { } lastUsed || step > lastUsed;
    }

    /// <summary>
    /// Sets the password of a new account.
    /// </summary>
    /// <param name="passwordHash">Hash of the password.</param>
    public void AssignPassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
    }

    /// <summary>
    /// Forgets that the verification code was sent, so a new one can be requested at once after a
    /// failed delivery.
    /// </summary>
    public void ForgetOtpDelivery()
    {
        OtpLastSentAt = null;
    }

    /// <summary>
    /// Tells whether a new verification code must wait for the resend cooldown.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="cooldown">Minimum time between two codes.</param>
    /// <returns><see langword="true"/> while the last code is too recent.</returns>
    public bool IsOtpResendCoolingDown(DateTimeOffset now, TimeSpan cooldown)
    {
        return now < OtpLastSentAt + cooldown;
    }

    /// <summary>
    /// Tells whether a new password reset code must wait for the resend cooldown.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="cooldown">Minimum time between two codes.</param>
    /// <returns><see langword="true"/> while the last code is too recent.</returns>
    public bool IsPasswordResetResendCoolingDown(DateTimeOffset now, TimeSpan cooldown)
    {
        return now < PasswordResetLastSentAt + cooldown;
    }

    /// <summary>
    /// Tells whether a new login code must wait for the resend cooldown.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="cooldown">Minimum time between two codes.</param>
    /// <returns><see langword="true"/> while the last code is too recent.</returns>
    public bool IsLoginCodeResendCoolingDown(DateTimeOffset now, TimeSpan cooldown)
    {
        return now < LoginCodeLastSentAt + cooldown;
    }

    /// <summary>
    /// Tells whether a second-factor challenge is the one the account has open.
    /// </summary>
    /// <param name="challengeId">Identifier of the challenge.</param>
    /// <returns><see langword="true"/> when it is the open challenge.</returns>
    public bool HasLoginChallenge(Guid challengeId)
    {
        return LoginChallengeId == challengeId;
    }

    /// <summary>
    /// Records the authenticator step of an accepted code, so the same code cannot be used twice.
    /// </summary>
    /// <param name="step">Time step of the accepted code.</param>
    public void RecordAuthenticatorStep(long step)
    {
        AuthenticatorLastUsedStep = step;
    }

    /// <summary>
    /// Forgets the failed second-factor attempts.
    /// </summary>
    public void ClearTwoFactorFailures()
    {
        TwoFactorFailedAttempts = 0;
    }

    /// <summary>
    /// Counts a wrong password and locks the account once it reaches the maximum; a locked
    /// account counts nothing more until its password is reset. Locking also closes the open
    /// second-factor challenge.
    /// </summary>
    /// <param name="maxFailedAttempts">Wrong passwords that lock the account.</param>
    /// <param name="now">Current time.</param>
    /// <returns><see langword="true"/> when this failure locked the account.</returns>
    public bool RecordPasswordFailure(int maxFailedAttempts, DateTimeOffset now)
    {
        if (PasswordLockedAt is not null)
        {
            return false;
        }

        PasswordFailedAttempts++;
        if (PasswordFailedAttempts < maxFailedAttempts)
        {
            return false;
        }

        PasswordLockedAt = now;
        ClearLoginChallenge();
        return true;
    }

    /// <summary>
    /// Changes the membership type.
    /// </summary>
    /// <param name="userTypeId">Identifier of the new membership type.</param>
    /// <param name="now">Current time.</param>
    public void ChangeType(Guid userTypeId, DateTimeOffset now)
    {
        UserTypeId = userTypeId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Grants or revokes the administrator rights.
    /// </summary>
    /// <param name="isAdmin">Whether the account is an administrator from now on.</param>
    /// <param name="now">Current time.</param>
    /// <returns><see langword="true"/> when the rights changed.</returns>
    public bool SetAdministrator(bool isAdmin, DateTimeOffset now)
    {
        if (IsAdmin == isAdmin)
        {
            return false;
        }

        IsAdmin = isAdmin;
        UpdatedAt = now;
        return true;
    }
}
