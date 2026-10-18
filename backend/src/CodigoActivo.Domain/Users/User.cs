using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Represents the persisted user domain entity and its relationships. A person is either an
/// independent account, which logs in and needs an email, a phone and a DNI or NIE, or a dependent
/// of a guardian, which has none of those and needs a birth date instead. Create them with
/// <see cref="CreateIndependent"/> and <see cref="CreateDependent"/>, and edit them with
/// <see cref="PlanProfileChange"/> and <see cref="ApplyProfileChange"/>, which own those rules, or
/// with <see cref="ApplyProfileChangeConfirmingEmail"/> when a new email must be confirmed first.
/// </summary>
public class User : AggregateRoot<UserId>
{
    /// <summary>
    /// Days an account may wait for the verification of its email before it is erased.
    /// </summary>
    public const int UnverifiedRetentionDays = 30;

    private readonly List<PromotionalConsentChange> promotionalConsentChanges = [];

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
    public EmailAddress? Email { get; private set; }

    /// <summary>
    /// Gets the move to the email the holder asked for. It replaces <see cref="Email"/> only once
    /// the code emailed to the new address is confirmed.
    /// </summary>
    public EmailChange EmailChange { get; private set; }

    /// <summary>
    /// Gets the phone value.
    /// </summary>
    public PhoneNumber? Phone { get; private set; }

    /// <summary>
    /// Gets the optional second contact phone of an independent account. It is never
    /// equal to <see cref="Phone"/> and is always unset for dependents.
    /// </summary>
    public PhoneNumber? SecondaryPhone { get; private set; }

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
    public SpanishNationalId? NationalId { get; private set; }

    /// <summary>
    /// Gets whether the user agreed to receive promotional content. Always
    /// <see langword="false"/> for dependents.
    /// </summary>
    public bool PromotionalConsent { get; private set; }

    /// <summary>
    /// Gets every change of <see cref="PromotionalConsent"/>, each with when and where the holder
    /// made it, which proves the consent behind each promotional email.
    /// </summary>
    public IReadOnlyCollection<PromotionalConsentChange> PromotionalConsentChanges =>
        promotionalConsentChanges;

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
    public UserId? ParentId { get; private set; }

    /// <summary>
    /// Gets the stage of the account.
    /// </summary>
    public UserStatus Status { get; private set; }

    /// <summary>
    /// Gets the relationship of the person with the association.
    /// </summary>
    public UserType UserType { get; private set; }

    /// <summary>
    /// Gets whether admin.
    /// </summary>
    public bool IsAdmin { get; private set; }

    /// <summary>
    /// Gets the emailed code that verifies the email of a new account.
    /// </summary>
    public OneTimeCode VerificationCode { get; private set; }

    /// <summary>
    /// Gets the emailed code that lets the holder choose a new password.
    /// </summary>
    public OneTimeCode PasswordResetCode { get; private set; }

    /// <summary>
    /// Gets the second factor required after the password. Every account has one.
    /// </summary>
    public TwoFactorMethod TwoFactorMethod { get; private set; } = TwoFactorMethod.Email;

    /// <summary>
    /// Gets the authenticator application, active or waiting for its first code.
    /// </summary>
    public Authenticator Authenticator { get; private set; }

    /// <summary>
    /// Gets the emailed login code of the open two-factor challenge.
    /// </summary>
    public OneTimeCode LoginCode { get; private set; }

    /// <summary>
    /// Gets the open second-factor challenge.
    /// </summary>
    public LoginChallenge LoginChallenge { get; private set; }

    /// <summary>
    /// Gets the wrong second-factor codes counted and the temporary lock they lead to.
    /// </summary>
    public TwoFactorLockout TwoFactorLockout { get; private set; }

    /// <summary>
    /// Gets the wrong passwords counted and the lock they lead to.
    /// </summary>
    public PasswordLockout PasswordLockout { get; private set; }

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
        UserId? id = null
    )
    {
        ArgumentNullException.ThrowIfNull(details);
        var account = new User
        {
            Id = id ?? UserId.New(),
            FirstName = string.Empty,
            LastName = string.Empty,
            Status = UserStatus.Pending,
            UserType = UserType.Participant,
            CreatedAt = now,
        };
        var change = account.PlanIndependentChange(details, guardianId: null);
        if (change.IsFailure)
        {
            return change.Error!;
        }

        account.Apply(change.Value, PromotionalConsentSource.Registration, now);
        account.Raise(new AccountRegistered(account.Id));
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
        UserId? id = null
    )
    {
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(details);
        if (guardian.ParentId is not null)
        {
            return Error.Validation(DomainErrorCode.UserParentIsMinor);
        }

        var dependent = new User
        {
            Id = id ?? UserId.New(),
            FirstName = string.Empty,
            LastName = string.Empty,
            ParentId = guardian.Id,
            Status = UserStatus.Dependent,
            UserType = UserType.Participant,
            CreatedAt = now,
        };
        var change = dependent.PlanDependentChange(details, guardianId: null, today);
        if (change.IsFailure)
        {
            return change.Error!;
        }

        dependent.Apply(change.Value, PromotionalConsentSource.Registration, now);
        dependent.Raise(new DependentAdded(dependent.Id, guardian.Id));
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
        UserId? guardianId,
        DateOnly today
    )
    {
        ArgumentNullException.ThrowIfNull(details);
        return ParentId is null
            ? PlanIndependentChange(details, guardianId)
            : PlanDependentChange(details, guardianId, today);
    }

    /// <summary>
    /// Applies a change planned for this account by <see cref="PlanProfileChange"/>. A new email
    /// replaces the stored one at once and drops any email change still waiting for confirmation.
    /// A new promotional consent is recorded as decided in the profile.
    /// </summary>
    /// <param name="change">Change planned for this account.</param>
    /// <param name="now">Current timestamp, stored as the update time.</param>
    /// <exception cref="ArgumentException">The change was planned for another account.</exception>
    public void ApplyProfileChange(ProfileChange change, DateTimeOffset now)
    {
        EnsurePlannedHere(change);
        var previousEmail = Email;
        if (change.NewEmail is not null)
        {
            ClearEmailChange();
        }

        Apply(change, PromotionalConsentSource.Profile, now);
        UpdatedAt = now;
        Raise(new ProfileChanged(Id));
        if (change.ReplacesContact)
        {
            Raise(
                new ContactDetailsReplaced(
                    Id,
                    previousEmail,
                    FirstName,
                    change.NewEmail,
                    change.ReplacesPhones
                )
            );
        }
    }

    /// <summary>
    /// Applies a change the holder of this account planned with <see cref="PlanProfileChange"/>
    /// except its new email: the account keeps the stored one until
    /// <see cref="ConfirmEmailChange"/> proves, with the code emailed to the new address, that the
    /// holder controls it. A later request replaces this one.
    /// </summary>
    /// <param name="change">Change planned for this account; it must replace the email.</param>
    /// <param name="codeHash">Hash of the code emailed to the new address.</param>
    /// <param name="now">Current timestamp, stored as the update time.</param>
    /// <param name="lifetime">How long the emailed code stays valid.</param>
    /// <exception cref="ArgumentException">
    /// The change was planned for another account or keeps the email.
    /// </exception>
    public void ApplyProfileChangeConfirmingEmail(
        ProfileChange change,
        string codeHash,
        DateTimeOffset now,
        TimeSpan lifetime
    )
    {
        EnsurePlannedHere(change);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        var newEmail =
            change.NewEmail
            ?? throw new ArgumentException("The profile change keeps the email.", nameof(change));
        EmailChange = EmailChange.Request(newEmail, codeHash, now, lifetime);
        ApplyKeepingEmail(change, PromotionalConsentSource.Profile, now);
        UpdatedAt = now;
        Raise(new ProfileChanged(Id));
        if (change.ReplacesPhones)
        {
            Raise(
                new ContactDetailsReplaced(
                    Id,
                    Email,
                    FirstName,
                    NewEmail: null,
                    PhonesReplaced: true
                )
            );
        }
    }

    /// <summary>
    /// Moves the account to the pending email once the code emailed to it was accepted.
    /// </summary>
    /// <param name="now">Current timestamp, stored as the update time.</param>
    /// <exception cref="InvalidOperationException">No email change is waiting.</exception>
    public void ConfirmEmailChange(DateTimeOffset now)
    {
        var previousEmail = Email;
        Email =
            EmailChange.NewEmail
            ?? throw new InvalidOperationException("No email change is waiting for confirmation.");
        ClearEmailChange();
        UpdatedAt = now;
        Raise(new ProfileChanged(Id));
        Raise(
            new ContactDetailsReplaced(Id, previousEmail, FirstName, Email, PhonesReplaced: false)
        );
    }

    private void EnsurePlannedHere(ProfileChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!ReferenceEquals(change.Account, this))
        {
            throw new ArgumentException(
                "The profile change was planned for another account.",
                nameof(change)
            );
        }
    }

    private void ClearEmailChange()
    {
        EmailChange = default;
    }

    private Result<ProfileChange> PlanIndependentChange(PersonDetails details, UserId? guardianId)
    {
        if (details.BirthDate is not null)
        {
            return Error.Validation(DomainErrorCode.UserBirthDateNotAllowedForAdult);
        }

        if (guardianId is not null)
        {
            return Error.Validation(DomainErrorCode.UserParentNotAllowedForAdult);
        }

        var nationalId = SpanishNationalId.Create(details.NationalId);
        if (nationalId.IsFailure)
        {
            return nationalId.Error!;
        }

        var contact = ContactDetails.From(details);
        if (contact.IsFailure)
        {
            return contact.Error!;
        }

        return new ProfileChange(this, details, contact.Value, nationalId.Value, birthDate: null);
    }

    private Result<ProfileChange> PlanDependentChange(
        PersonDetails details,
        UserId? guardianId,
        DateOnly today
    )
    {
        if (guardianId is { } requested && requested != ParentId)
        {
            return Error.Forbidden(DomainErrorCode.UserParentReassignmentForbidden);
        }

        if (details.BirthDate is not { } birthDate)
        {
            return Error.Validation(DomainErrorCode.UserChildBirthDateRequired);
        }

        if (birthDate != BirthDate)
        {
            if (birthDate > today)
            {
                return Error.Validation(DomainErrorCode.UserChildBirthDateInFuture);
            }

            if (!birthDate.IsMinor(today))
            {
                return Error.Validation(DomainErrorCode.UserChildBirthDateNotMinor);
            }
        }

        return new ProfileChange(this, details, contact: null, nationalId: null, birthDate);
    }

    private void Apply(ProfileChange change, PromotionalConsentSource source, DateTimeOffset now)
    {
        ApplyKeepingEmail(change, source, now);
        if (change.Contact is { } contact)
        {
            Email = contact.Email;
        }
    }

    private void ApplyKeepingEmail(
        ProfileChange change,
        PromotionalConsentSource source,
        DateTimeOffset now
    )
    {
        FirstName = change.FirstName;
        LastName = change.LastName;
        Gender = change.Gender;
        NationalId = change.NationalId;
        ChangePromotionalConsent(change.PromotionalConsent, source, now);
        if (change.Contact is { } contact)
        {
            Phone = contact.Phone;
            SecondaryPhone = contact.SecondaryPhone;
            return;
        }

        BirthDate = change.BirthDate;
    }

    /// <summary>
    /// Withdraws the promotional consent through the unsubscribe link of a promotional email or
    /// the unsubscribe button of the mail client. An account without the consent stays as it is,
    /// so the link can be followed again.
    /// </summary>
    /// <param name="source">
    /// <see cref="PromotionalConsentSource.UnsubscribeLink"/> or
    /// <see cref="PromotionalConsentSource.OneClickUnsubscribe"/>.
    /// </param>
    /// <param name="now">Current timestamp, stored as the time of the change.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="source"/> is not one of the unsubscribe sources.
    /// </exception>
    public void WithdrawPromotionalConsent(PromotionalConsentSource source, DateTimeOffset now)
    {
        if (
            source
            is not (
                PromotionalConsentSource.UnsubscribeLink
                or PromotionalConsentSource.OneClickUnsubscribe
            )
        )
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        if (!ChangePromotionalConsent(false, source, now))
        {
            return;
        }

        UpdatedAt = now;
        Raise(new ProfileChanged(Id));
    }

    private bool ChangePromotionalConsent(
        bool granted,
        PromotionalConsentSource source,
        DateTimeOffset now
    )
    {
        if (granted == PromotionalConsent)
        {
            return false;
        }

        PromotionalConsent = granted;
        promotionalConsentChanges.Add(new PromotionalConsentChange(Id, granted, source, now));
        return true;
    }

    /// <summary>
    /// Determines whether sue otp.
    /// </summary>
    /// <param name="codeHash">The code hash value.</param>
    /// <param name="now">Current timestamp used to calculate replenishment.</param>
    /// <param name="lifetime">The lifetime value.</param>
    public void IssueOtp(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        VerificationCode = OneTimeCode.Issue(codeHash, now, lifetime);
    }

    /// <summary>
    /// Clears the stored otp.
    /// </summary>
    public void ClearOtp()
    {
        VerificationCode = default;
    }

    /// <summary>
    /// Determines whether sue password reset code.
    /// </summary>
    /// <param name="codeHash">The code hash value.</param>
    /// <param name="now">Current timestamp used to calculate replenishment.</param>
    /// <param name="lifetime">The lifetime value.</param>
    public void IssuePasswordResetCode(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        PasswordResetCode = OneTimeCode.Issue(codeHash, now, lifetime);
    }

    /// <summary>
    /// Clears the stored password reset code.
    /// </summary>
    public void ClearPasswordResetCode()
    {
        PasswordResetCode = default;
    }

    /// <summary>
    /// Resets the password using the supplied value.
    /// </summary>
    /// <param name="passwordHash">The password hash value.</param>
    /// <param name="now">Current timestamp used to calculate replenishment.</param>
    public void ResetPassword(string passwordHash, DateTimeOffset now)
    {
        ReplacePassword(passwordHash, now);
        Raise(new PasswordReset(Id));
    }

    /// <summary>
    /// Replaces the password at the request of the holder, who proved the current one.
    /// </summary>
    /// <param name="passwordHash">Hash of the new password.</param>
    /// <param name="now">Current timestamp.</param>
    public void ChangePassword(string passwordHash, DateTimeOffset now)
    {
        ReplacePassword(passwordHash, now);
        Raise(new PasswordChanged(Id));
    }

    private void ReplacePassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        ClearPasswordResetCode();
        PasswordLockout = default;
        ClearLoginChallenge();
        UpdatedAt = now;
    }

    /// <summary>
    /// Tells whether the account is a dependent in the care of a guardian.
    /// </summary>
    /// <param name="guardianId">Identifier of the guardian.</param>
    /// <returns><see langword="true"/> when the guardian is responsible for this account.</returns>
    public bool IsDependentOf(UserId guardianId)
    {
        return ParentId == guardianId;
    }

    /// <summary>
    /// Forgets the consecutive wrong passwords counted so far. Wrong passwords are counted by
    /// <see cref="RecordPasswordFailure"/> while the account row is locked, so parallel attempts
    /// cannot lose each other's increments.
    /// </summary>
    public void ClearPasswordFailures()
    {
        PasswordLockout = PasswordLockout.WithoutFailures();
    }

    /// <summary>
    /// Activates the account once its email is verified, and forgets the verification code. Only
    /// an account that still waits for that verification can be activated this way.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>A failed result when the account no longer waits for its verification.</returns>
    public Result Verify(DateTimeOffset now)
    {
        if (!IsPendingVerification)
        {
            return Error.Validation(DomainErrorCode.UserNotPendingVerification);
        }

        Status = UserStatus.Active;
        ClearOtp();
        UpdatedAt = now;
        return Result.Success();
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
    /// Decides whether a second-factor code may be emailed to the account now: it needs an email,
    /// email as its second factor and a second factor that is not locked.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <returns>A failed result naming the first rule the account breaks.</returns>
    public Result MayReceiveLoginCode(DateTimeOffset now)
    {
        if (Email is null)
        {
            return Error.Conflict(DomainErrorCode.UserContactInfoRequired);
        }

        if (TwoFactorMethod is not TwoFactorMethod.Email)
        {
            return Error.Conflict(DomainErrorCode.TwoFactorResendNotAllowed);
        }

        return TwoFactorLockout.IsLocked(now)
            ? Error.Forbidden(DomainErrorCode.TwoFactorLocked)
            : Result.Success();
    }

    /// <summary>
    /// Stores a new emailed second-factor code, replacing any previous one, when
    /// <see cref="MayReceiveLoginCode"/> allows it.
    /// </summary>
    /// <param name="codeHash">Hash of the code that was emailed.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="lifetime">How long the code stays valid.</param>
    /// <returns>A failed result when the account may not receive a code.</returns>
    public Result IssueLoginCode(string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        var allowed = MayReceiveLoginCode(now);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        LoginCode = OneTimeCode.Issue(codeHash, now, lifetime);
        return Result.Success();
    }

    /// <summary>
    /// Clears the emailed login code and its timestamps.
    /// </summary>
    public void ClearLoginCode()
    {
        LoginCode = default;
    }

    /// <summary>
    /// Opens a second-factor challenge under a new identifier, replacing any open one so the
    /// cookie of an earlier password step stops being accepted.
    /// </summary>
    /// <param name="challengeId">Identifier the challenge cookie will carry.</param>
    /// <param name="now">Current timestamp.</param>
    public void StartLoginChallenge(LoginChallengeId challengeId, DateTimeOffset now)
    {
        LoginChallenge = LoginChallenge.Start(challengeId, now);
    }

    /// <summary>
    /// Closes the open second-factor challenge, if any, so its cookie is refused from now on.
    /// </summary>
    public void ClearLoginChallenge()
    {
        LoginChallenge = default;
    }

    /// <summary>
    /// Counts a wrong second-factor code and locks verification once the limit is reached,
    /// raising <see cref="TwoFactorLocked"/>.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    /// <param name="maxFailedAttempts">Failures allowed before locking.</param>
    /// <param name="lockoutDuration">How long the lock lasts.</param>
    public void RecordTwoFactorFailure(
        DateTimeOffset now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration
    )
    {
        var locks = TwoFactorLockout.LocksOnNextFailure(maxFailedAttempts);
        TwoFactorLockout = TwoFactorLockout.AfterFailure(now, maxFailedAttempts, lockoutDuration);
        if (!locks)
        {
            return;
        }

        ClearLoginCode();
        ClearLoginChallenge();
        Raise(new TwoFactorLocked(Id));
    }

    /// <summary>
    /// Completes a login after the second factor was accepted.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    public void CompleteTwoFactorLogin(DateTimeOffset now)
    {
        ClearLoginCode();
        ClearLoginChallenge();
        TwoFactorLockout = default;
        RegisterLogin(now);
    }

    /// <summary>
    /// Stores the protected secret of an authenticator that still has to be confirmed. An account
    /// that already uses an authenticator must return to email first, which takes a current code of
    /// that authenticator, so a session and the password alone can never replace it.
    /// </summary>
    /// <param name="protectedKey">Protected shared secret.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="lifetime">How long the enrollment can be confirmed.</param>
    /// <returns>A failed result when the account already uses an authenticator.</returns>
    public Result BeginAuthenticatorSetup(
        string protectedKey,
        DateTimeOffset now,
        TimeSpan lifetime
    )
    {
        if (TwoFactorMethod is TwoFactorMethod.Authenticator)
        {
            return Error.Conflict(DomainErrorCode.AuthenticatorAlreadyEnabled);
        }

        Authenticator = Authenticator.BeginEnrollment(protectedKey, now, lifetime);
        return Result.Success();
    }

    /// <summary>
    /// Promotes the pending authenticator to the active second factor. Only an enrollment that is
    /// still waiting can be promoted, and it never replaces an authenticator that is already
    /// active.
    /// </summary>
    /// <param name="usedStep">Time step of the code that confirmed the enrollment.</param>
    /// <param name="now">Current timestamp.</param>
    /// <returns>
    /// A failed result when the account already uses an authenticator or no enrollment is waiting.
    /// </returns>
    public Result EnableAuthenticator(long usedStep, DateTimeOffset now)
    {
        if (TwoFactorMethod is TwoFactorMethod.Authenticator)
        {
            return Error.Conflict(DomainErrorCode.AuthenticatorAlreadyEnabled);
        }

        if (!Authenticator.HasPendingEnrollment(now))
        {
            return Error.Validation(DomainErrorCode.AuthenticatorSetupExpired);
        }

        Authenticator = Authenticator.Activate(usedStep);
        TwoFactorMethod = TwoFactorMethod.Authenticator;
        ClearLoginCode();
        UpdatedAt = now;
        Raise(new AuthenticatorEnabled(Id));
        return Result.Success();
    }

    /// <summary>
    /// Goes back to email as the second factor at the request of the holder, who proved they still
    /// control the authenticator, and forgets the failed attempts.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    public void DisableAuthenticator(DateTimeOffset now)
    {
        UseEmailTwoFactor(now);
        ClearTwoFactorFailures();
        Raise(new AuthenticatorDisabled(Id));
    }

    /// <summary>
    /// Makes email the second factor again and forgets any authenticator, active or pending.
    /// </summary>
    /// <param name="now">Current timestamp.</param>
    public void UseEmailTwoFactor(DateTimeOffset now)
    {
        TwoFactorMethod = TwoFactorMethod.Email;
        Authenticator = default;
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
        TwoFactorLockout = default;
        if (leftAuthenticator)
        {
            Raise(new TwoFactorReset(Id));
        }

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
        EmailAddress email,
        string passwordHash,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Id = InitialAdministrator.Id,
            FirstName = "Administrador",
            LastName = Organization.Name,
            Email = email,
            PasswordHash = passwordHash,
            NationalId = SpanishNationalId.FromDniNumber(0),
            Gender = Gender.Other,
            Status = UserStatus.Active,
            UserType = UserType.Member,
            IsAdmin = true,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Gets a value indicating whether the account can open sessions: it is active and has a
    /// password.
    /// </summary>
    public bool CanSignIn => Status == UserStatus.Active && PasswordHash is not null;

    /// <summary>
    /// Gets a value indicating whether an administrator blocked the account.
    /// </summary>
    public bool IsBlocked => Status == UserStatus.Blocked;

    /// <summary>
    /// Gets a value indicating whether the account is a dependent, which never signs in by itself.
    /// </summary>
    public bool IsDependent => Status == UserStatus.Dependent;

    /// <summary>
    /// Gets a value indicating whether the account still waits for the verification of its email.
    /// </summary>
    public bool IsPendingVerification => Status == UserStatus.Pending;

    /// <summary>
    /// Tells up to which registration moment an account nobody verified has waited longer than
    /// <see cref="UnverifiedRetentionDays"/>, and is erased.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The latest registration moment of an expired unverified account.</returns>
    public static DateTimeOffset UnverifiedPurgeCutoff(DateTimeOffset now)
    {
        return now.AddDays(-UnverifiedRetentionDays);
    }

    /// <summary>
    /// Tells whether nobody verified the account within <see cref="UnverifiedRetentionDays"/> of its
    /// registration.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns><see langword="true"/> when the account is still unverified and expired.</returns>
    public bool HasVerificationExpired(DateTimeOffset now)
    {
        return IsPendingVerification && CreatedAt <= UnverifiedPurgeCutoff(now);
    }

    /// <summary>
    /// Gets a value indicating whether the account keeps its email against anyone else who claims
    /// it. An account nobody verified does not: a new registration with the address replaces it,
    /// and so does an account whose holder confirms the address as their new email.
    /// </summary>
    public bool OwnsEmail => !IsPendingVerification;

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
        VerificationCode = VerificationCode.WithoutDelivery();
    }

    /// <summary>
    /// Tells whether a second-factor challenge can still be answered: it is the one the account has
    /// open, it is young enough, and the account may still sign in and is not locked by wrong
    /// passwords.
    /// </summary>
    /// <param name="challengeId">Identifier of the challenge.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="lifetime">How long a challenge can be answered after it opens.</param>
    /// <returns><see langword="true"/> when the challenge can be answered.</returns>
    public bool HasOpenLoginChallenge(
        LoginChallengeId challengeId,
        DateTimeOffset now,
        TimeSpan lifetime
    )
    {
        return CanSignIn
            && !PasswordLockout.IsLocked
            && LoginChallenge.IsOpen(challengeId, now, lifetime);
    }

    /// <summary>
    /// Records the authenticator step of an accepted code, so the same code cannot be used twice.
    /// </summary>
    /// <param name="step">Time step of the accepted code.</param>
    public void RecordAuthenticatorStep(long step)
    {
        Authenticator = Authenticator.WithUsedStep(step);
    }

    /// <summary>
    /// Forgets the failed second-factor attempts.
    /// </summary>
    public void ClearTwoFactorFailures()
    {
        TwoFactorLockout = TwoFactorLockout.WithoutFailures();
    }

    /// <summary>
    /// Counts a wrong password and locks the account once it reaches the maximum, raising
    /// <see cref="PasswordLocked"/>; a locked account counts nothing more until its password is
    /// reset. Locking also closes the open second-factor challenge.
    /// </summary>
    /// <param name="maxFailedAttempts">Wrong passwords that lock the account.</param>
    /// <param name="now">Current time.</param>
    public void RecordPasswordFailure(int maxFailedAttempts, DateTimeOffset now)
    {
        var locks = PasswordLockout.LocksOnNextFailure(maxFailedAttempts);
        PasswordLockout = PasswordLockout.AfterFailure(maxFailedAttempts, now);
        if (!locks)
        {
            return;
        }

        ClearLoginChallenge();
        Raise(new PasswordLocked(Id));
    }

    /// <summary>
    /// Changes the membership type.
    /// </summary>
    /// <param name="userType">New membership type.</param>
    /// <param name="now">Current time.</param>
    public void ChangeType(UserType userType, DateTimeOffset now)
    {
        UserType = userType;
        UpdatedAt = now;
        Raise(new UserTypeChanged(Id));
    }

    /// <summary>
    /// Grants or revokes the administrator rights. A dependent, who never signs in, never gets them,
    /// and the initial administrator never loses them.
    /// </summary>
    /// <param name="isAdmin">Whether the account is an administrator from now on.</param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// Whether the rights changed, or a failed result when they would be given to a dependent or
    /// taken from the initial administrator.
    /// </returns>
    public Result<bool> SetAdministrator(bool isAdmin, DateTimeOffset now)
    {
        if (isAdmin && IsDependent)
        {
            return Error.Validation(DomainErrorCode.UserDependentCannotBeAdmin);
        }

        if (!isAdmin)
        {
            var revocable = InitialAdministrator.EnsureMayLoseAdminRights(Id);
            if (revocable.IsFailure)
            {
                return revocable.Error!;
            }
        }

        if (IsAdmin == isAdmin)
        {
            return false;
        }

        IsAdmin = isAdmin;
        UpdatedAt = now;
        Raise(new AdministratorRightsChanged(Id, isAdmin));
        return true;
    }
}
