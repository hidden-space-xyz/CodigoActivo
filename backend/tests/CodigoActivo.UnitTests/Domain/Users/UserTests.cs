using AwesomeAssertions;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset Seeded = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<Guid, bool, bool, bool> StatusFlags =>
        new()
        {
            { KnownIds.UserStatusTypes.Pending, false, false, true },
            { KnownIds.UserStatusTypes.Active, false, false, false },
            { KnownIds.UserStatusTypes.Blocked, true, false, false },
            { KnownIds.UserStatusTypes.Dependent, false, true, false },
        };

    public static TheoryData<Guid, string?, bool> SignInStates =>
        new()
        {
            { KnownIds.UserStatusTypes.Active, "hash", true },
            { KnownIds.UserStatusTypes.Active, null, false },
            { KnownIds.UserStatusTypes.Pending, "hash", false },
            { KnownIds.UserStatusTypes.Blocked, "hash", false },
            { KnownIds.UserStatusTypes.Dependent, "hash", false },
        };

    private static User UserWithStatus(Guid statusId, string? passwordHash = "hash")
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Ada",
                LastName = "Lovelace",
                PasswordHash = passwordHash,
                Status = CatalogIds.UserStatuses.ValueOf(statusId),
                CreatedAt = Seeded,
            }
        );
    }

    private static User NewSignInUser()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { Status = UserStatus.Active, PasswordHash = "hash" });
        return user;
    }

    private static User NewPendingUser()
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Ada",
                LastName = "Lovelace",
                Email = "ada@test.local",
                BirthDate = new DateOnly(1990, 1, 1),
                Status = UserStatus.Pending,
                VerificationCode = new OneTimeCode("ABCDEF", Seeded, Seeded),
                CreatedAt = Seeded,
            }
        );
    }

    [Fact]
    public void VerifyPendingUserActivatesAccountAndClearsOtp()
    {
        var user = NewPendingUser();

        user.Verify(Now);

        user.Status.Should().Be(UserStatus.Active);
        user.IsPendingVerification.Should().BeFalse();
        user.VerificationCode.Hash.Should().BeNull();
        user.VerificationCode.ExpiresAt.Should().BeNull();
        user.VerificationCode.LastSentAt.Should().BeNull();
        user.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void VerifyPendingUserLeavesLastLoginUntouched()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { LastLoginAt = default(DateTimeOffset?) });

        user.Verify(Now);

        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void VerifyActiveUserIsRefusedAndKeepsItsState()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { Status = UserStatus.Active });

        var verified = user.Verify(Now);

        verified.ShouldFail(ErrorKind.Validation, DomainErrorCode.UserNotPendingVerification);
        user.VerificationCode.Hash.Should().Be("ABCDEF");
        user.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void IssueOtpHashAndLifetimeStoresHashAndTimestamps()
    {
        var user = NewPendingUser();

        user.IssueOtp("NEWHASH", Now, TimeSpan.FromMinutes(15));

        user.VerificationCode.Hash.Should().Be("NEWHASH");
        user.VerificationCode.ExpiresAt.Should().Be(Now.AddMinutes(15));
        user.VerificationCode.LastSentAt.Should().Be(Now);
    }

    [Fact]
    public void RegisterLoginPendingUserStampsSuppliedLastLoginTime()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { LastLoginAt = default(DateTimeOffset?) });

        user.RegisterLogin(Now);

        user.LastLoginAt.Should().Be(Now);
    }

    [Fact]
    public void RegisterLoginPendingUserDoesNotChangeStatusOrOtp()
    {
        var user = NewPendingUser();
        var status = user.Status;
        var otpHash = user.VerificationCode.Hash;

        user.RegisterLogin(Now);

        user.Status.Should().Be(status);
        user.VerificationCode.Hash.Should().Be(otpHash);
        user.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void NewUserDefaultsToEmailAsSecondFactor()
    {
        NewPendingUser().TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
    }

    [Fact]
    public void IssueLoginCodeStoresHashAndTimestamps()
    {
        var user = NewPendingUser();

        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));

        user.LoginCode.Hash.Should().Be("HASH");
        user.LoginCode.ExpiresAt.Should().Be(Now.AddMinutes(10));
        user.LoginCode.LastSentAt.Should().Be(Now);
        user.LoginCode.IsRecent(Now.AddSeconds(30), TimeSpan.FromSeconds(60)).Should().BeTrue();
        user.LoginCode.IsRecent(Now.AddSeconds(61), TimeSpan.FromSeconds(60)).Should().BeFalse();
        user.LoginCode.IsRecent(Now.AddMinutes(11), TimeSpan.FromMinutes(20)).Should().BeFalse();
    }

    [Fact]
    public void IssueLoginCodeAccountWithoutEmailIsRefused()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { Email = default(string) });

        var issued = user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));

        issued.ShouldFail(ErrorKind.Conflict, DomainErrorCode.UserContactInfoRequired);
        user.LoginCode.Hash.Should().BeNull();
    }

    [Fact]
    public void IssueLoginCodeAuthenticatorAccountIsRefused()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { TwoFactorMethod = TwoFactorMethod.Authenticator });

        var issued = user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));

        issued.ShouldFail(ErrorKind.Conflict, DomainErrorCode.TwoFactorResendNotAllowed);
        user.LoginCode.Hash.Should().BeNull();
    }

    [Fact]
    public void IssueLoginCodeLockedSecondFactorIsRefused()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(
            user,
            new { TwoFactorLockout = new { LockedUntil = Now.AddMinutes(5) } }
        );

        var issued = user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));

        issued.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.TwoFactorLocked);
        user.LoginCode.Hash.Should().BeNull();
        user.MayReceiveLoginCode(Now.AddMinutes(5)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CompleteTwoFactorLoginForgetsLoginCodeHashAndTimestamps()
    {
        var user = NewPendingUser();
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));

        user.CompleteTwoFactorLogin(Now);

        user.LoginCode.Hash.Should().BeNull();
        user.LoginCode.ExpiresAt.Should().BeNull();
        user.LoginCode.LastSentAt.Should().BeNull();
        user.LoginCode.IsRecent(Now, TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void RecordTwoFactorFailureBelowTheLimitOnlyCounts()
    {
        var user = NewPendingUser();
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));

        user.RecordTwoFactorFailure(Now, 3, TimeSpan.FromMinutes(15));

        user.TwoFactorLockout.FailedAttempts.Should().Be(1);
        user.TwoFactorLockout.LockedUntil.Should().BeNull();
        user.TwoFactorLockout.IsLocked(Now).Should().BeFalse();
        user.LoginCode.Hash.Should().Be("HASH");
        user.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void RecordTwoFactorFailureAtTheLimitLocksResetsCounterDiscardsTheCodeAndRaisesTheLock()
    {
        var user = NewPendingUser();
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));
        user.StartLoginChallenge(LoginChallengeId.New(), Now);
        Persisted.Overwrite(user, new { TwoFactorLockout = new { FailedAttempts = 2 } });

        user.RecordTwoFactorFailure(Now, 3, TimeSpan.FromMinutes(15));

        user.TwoFactorLockout.FailedAttempts.Should().Be(0);
        user.TwoFactorLockout.LockedUntil.Should().Be(Now.AddMinutes(15));
        user.TwoFactorLockout.IsLocked(Now.AddMinutes(14)).Should().BeTrue();
        user.TwoFactorLockout.IsLocked(Now.AddMinutes(15)).Should().BeFalse();
        user.LoginCode.Hash.Should().BeNull();
        user.LoginChallenge.Id.Should().BeNull();
        user.PullDomainEvents().Should().Equal(new TwoFactorLocked(user.Id));
    }

    [Fact]
    public void StartLoginChallengeReplacesTheIdentifierOfAnyOpenChallenge()
    {
        var user = NewPendingUser();
        var first = LoginChallengeId.New();
        var second = LoginChallengeId.New();

        user.StartLoginChallenge(first, Now);
        user.LoginChallenge.Id.Should().Be(first);

        user.StartLoginChallenge(second, Now);
        user.LoginChallenge.Id.Should().Be(second);

        user.ClearLoginChallenge();
        user.LoginChallenge.Id.Should().BeNull();
    }

    [Fact]
    public void RecordTwoFactorFailureBelowTheLimitKeepsTheOpenChallenge()
    {
        var user = NewPendingUser();
        var challengeId = LoginChallengeId.New();
        user.StartLoginChallenge(challengeId, Now);

        user.RecordTwoFactorFailure(Now, 3, TimeSpan.FromMinutes(15));

        user.LoginChallenge.Id.Should().Be(challengeId);
    }

    [Fact]
    public void ResetPasswordClosesAnyOpenChallenge()
    {
        var user = NewPendingUser();
        user.StartLoginChallenge(LoginChallengeId.New(), Now);

        user.ResetPassword("new-hash", Now);

        user.LoginChallenge.Id.Should().BeNull();
    }

    [Fact]
    public void CompleteTwoFactorLoginClearsChallengeStateAndStampsLogin()
    {
        var user = NewPendingUser();
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));
        user.StartLoginChallenge(LoginChallengeId.New(), Now);
        Persisted.Overwrite(
            user,
            new { TwoFactorLockout = new { FailedAttempts = 2, LockedUntil = Now.AddMinutes(-1) } }
        );

        user.CompleteTwoFactorLogin(Now);

        user.LoginCode.Hash.Should().BeNull();
        user.LoginChallenge.Id.Should().BeNull();
        user.TwoFactorLockout.FailedAttempts.Should().Be(0);
        user.TwoFactorLockout.LockedUntil.Should().BeNull();
        user.LastLoginAt.Should().Be(Now);
    }

    [Fact]
    public void BeginAuthenticatorSetupStoresPendingKeyUntilItExpires()
    {
        var user = NewPendingUser();

        user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));

        user.Authenticator.PendingKey.Should().Be("PENDING");
        user.Authenticator.PendingExpiresAt.Should().Be(Now.AddMinutes(15));
        user.Authenticator.HasPendingEnrollment(Now.AddMinutes(14)).Should().BeTrue();
        user.Authenticator.HasPendingEnrollment(Now.AddMinutes(15)).Should().BeFalse();
        user.TwoFactorMethod.Should()
            .Be(TwoFactorMethod.Email, "setup does not change the factor yet");
    }

    [Fact]
    public void EnableAuthenticatorPromotesPendingKeyAndSwitchesTheFactor()
    {
        var user = NewPendingUser();
        user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));

        user.EnableAuthenticator(42, Now);

        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Authenticator);
        user.Authenticator.Key.Should().Be("PENDING");
        user.Authenticator.LastUsedStep.Should().Be(42);
        user.Authenticator.PendingKey.Should().BeNull();
        user.Authenticator.PendingExpiresAt.Should().BeNull();
        user.LoginCode.Hash.Should().BeNull();
        user.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void BeginAuthenticatorSetupActiveAuthenticatorIsRefusedWithoutAPendingKey()
    {
        var user = NewPendingUser();
        user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));
        user.EnableAuthenticator(42, Now);

        var begun = user.BeginAuthenticatorSetup("ANOTHER", Now, TimeSpan.FromMinutes(15));

        begun.ShouldFail(ErrorKind.Conflict, DomainErrorCode.AuthenticatorAlreadyEnabled);
        user.Authenticator.PendingKey.Should().BeNull();
        user.Authenticator.Key.Should().Be("PENDING");
    }

    [Fact]
    public void EnableAuthenticatorActiveAuthenticatorIsNeverReplaced()
    {
        var user = NewPendingUser();
        user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));
        user.EnableAuthenticator(42, Now);
        Persisted.Overwrite(
            user,
            new
            {
                Authenticator = new
                {
                    PendingKey = "ANOTHER",
                    PendingExpiresAt = Now.AddMinutes(15),
                },
            }
        );

        var enabled = user.EnableAuthenticator(43, Now.AddMinutes(1));

        enabled.ShouldFail(ErrorKind.Conflict, DomainErrorCode.AuthenticatorAlreadyEnabled);
        user.Authenticator.Key.Should().Be("PENDING");
        user.Authenticator.LastUsedStep.Should().Be(42);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnableAuthenticatorNoWaitingEnrollmentIsRefused(bool begunAndExpired)
    {
        var user = NewPendingUser();
        if (begunAndExpired)
        {
            user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));
        }

        var enabled = user.EnableAuthenticator(42, Now.AddMinutes(15));

        enabled.ShouldFail(ErrorKind.Validation, DomainErrorCode.AuthenticatorSetupExpired);
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.Authenticator.Key.Should().BeNull();
    }

    [Fact]
    public void DisableAuthenticatorForgetsActiveAndPendingAuthenticators()
    {
        var user = NewPendingUser();
        user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));
        user.EnableAuthenticator(42, Now);
        Persisted.Overwrite(
            user,
            new
            {
                Authenticator = new
                {
                    PendingKey = "ANOTHER",
                    PendingExpiresAt = Now.AddMinutes(15),
                },
            }
        );

        user.DisableAuthenticator(Now.AddHours(1));

        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.Authenticator.Key.Should().BeNull();
        user.Authenticator.LastUsedStep.Should().BeNull();
        user.Authenticator.PendingKey.Should().BeNull();
        user.Authenticator.PendingExpiresAt.Should().BeNull();
        user.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void ClearPasswordFailuresForgetsTheCountWithoutLiftingAnExistingLock()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(
            user,
            new { PasswordLockout = new { FailedAttempts = 5, LockedAt = Now } }
        );

        user.ClearPasswordFailures();

        user.PasswordLockout.FailedAttempts.Should().Be(0);
        user.PasswordLockout.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void ResetPasswordClearsTheLockAndTheFailureCount()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(
            user,
            new { PasswordLockout = new { FailedAttempts = 5, LockedAt = Now } }
        );
        user.IssuePasswordResetCode("HASH", Now, TimeSpan.FromMinutes(15));

        user.ResetPassword("new-hash", Now.AddMinutes(1));

        user.PasswordHash.Should().Be("new-hash");
        user.PasswordLockout.FailedAttempts.Should().Be(0);
        user.PasswordLockout.LockedAt.Should().BeNull();
        user.PasswordLockout.IsLocked.Should().BeFalse();
        user.PasswordResetCode.Hash.Should().BeNull();
        user.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void ResetTwoFactorLeavesAPasswordLockInPlace()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(
            user,
            new { PasswordLockout = new { FailedAttempts = 5, LockedAt = Now } }
        );

        var leftAuthenticator = user.ResetTwoFactor(Now);

        leftAuthenticator.Should().BeFalse();
        user.PasswordLockout.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void ResetTwoFactorReturnsEverySecondFactorFieldToItsDefault()
    {
        var user = NewPendingUser();
        user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));
        user.EnableAuthenticator(42, Now);
        Persisted.Overwrite(
            user,
            new
            {
                LoginCode = new { Hash = "HASH" },
                TwoFactorLockout = new { FailedAttempts = 4, LockedUntil = Now.AddMinutes(10) },
            }
        );

        var leftAuthenticator = user.ResetTwoFactor(Now);

        leftAuthenticator.Should().BeTrue();
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.Authenticator.Key.Should().BeNull();
        user.LoginCode.Hash.Should().BeNull();
        user.TwoFactorLockout.FailedAttempts.Should().Be(0);
        user.TwoFactorLockout.LockedUntil.Should().BeNull();
        user.TwoFactorLockout.IsLocked(Now).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(StatusFlags))]
    public void StatusFlagsEachStatusRaisesOnlyItsOwnFlag(
        Guid statusId,
        bool blocked,
        bool dependent,
        bool pending
    )
    {
        var user = UserWithStatus(statusId);

        user.IsBlocked.Should().Be(blocked);
        user.IsDependent.Should().Be(dependent);
        user.IsPendingVerification.Should().Be(pending);
        user.OwnsEmail.Should().Be(!pending);
    }

    [Theory]
    [MemberData(nameof(SignInStates))]
    public void CanSignInOnlyAnActiveAccountWithAPasswordMaySignIn(
        Guid statusId,
        string? passwordHash,
        bool expected
    )
    {
        UserWithStatus(statusId, passwordHash).CanSignIn.Should().Be(expected);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void IssueOtpIssuedCodeIsUsableUntilItExpires(int minutesLater, bool usable)
    {
        var user = NewPendingUser();
        user.IssueOtp("OTP", Now, TimeSpan.FromMinutes(10));

        user.VerificationCode.UsableHash(Now.AddMinutes(minutesLater))
            .Should()
            .Be(usable ? "OTP" : null);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void IssueLoginCodeIssuedCodeIsUsableUntilItExpires(int minutesLater, bool usable)
    {
        var user = NewPendingUser();
        user.IssueLoginCode("LOGIN", Now, TimeSpan.FromMinutes(10));

        user.LoginCode.UsableHash(Now.AddMinutes(minutesLater))
            .Should()
            .Be(usable ? "LOGIN" : null);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void IssuePasswordResetCodeIssuedCodeIsUsableUntilItExpires(
        int minutesLater,
        bool usable
    )
    {
        var user = NewPendingUser();
        user.IssuePasswordResetCode("RESET", Now, TimeSpan.FromMinutes(10));

        user.PasswordResetCode.UsableHash(Now.AddMinutes(minutesLater))
            .Should()
            .Be(usable ? "RESET" : null);
    }

    [Fact]
    public void UsableCodeHashesNoCodeIssuedReturnNothing()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { VerificationCode = default(OneTimeCode) });

        user.VerificationCode.UsableHash(Now).Should().BeNull();
        user.LoginCode.UsableHash(Now).Should().BeNull();
        user.PasswordResetCode.UsableHash(Now).Should().BeNull();
    }

    [Theory]
    [InlineData(null, 7L, true)]
    [InlineData(6L, 7L, true)]
    [InlineData(7L, 7L, false)]
    [InlineData(8L, 7L, false)]
    public void RecordAuthenticatorStepOnlyAStepNewerThanTheLastUsedIsAccepted(
        long? lastUsedStep,
        long step,
        bool expected
    )
    {
        var user = NewPendingUser();
        if (lastUsedStep is { } used)
        {
            user.RecordAuthenticatorStep(used);
        }

        user.Authenticator.Accepts(step).Should().Be(expected);
    }

    [Fact]
    public void RecordAuthenticatorStepRemembersTheStepAgainstReplays()
    {
        var user = NewPendingUser();

        user.RecordAuthenticatorStep(42);

        user.Authenticator.LastUsedStep.Should().Be(42);
        user.Authenticator.Accepts(42).Should().BeFalse();
        user.Authenticator.Accepts(43).Should().BeTrue();
    }

    [Fact]
    public void RecordPasswordFailureBelowTheLimitOnlyCounts()
    {
        var user = NewPendingUser();
        var challengeId = LoginChallengeId.New();
        user.StartLoginChallenge(challengeId, Now);

        user.RecordPasswordFailure(3, Now);

        user.PasswordLockout.FailedAttempts.Should().Be(1);
        user.PasswordLockout.LockedAt.Should().BeNull();
        user.PasswordLockout.IsLocked.Should().BeFalse();
        user.LoginChallenge.Id.Should().Be(challengeId);
        user.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void RecordPasswordFailureReachingTheLimitLocksClosesTheChallengeAndRaisesTheLock()
    {
        var user = NewPendingUser();
        user.StartLoginChallenge(LoginChallengeId.New(), Now);
        Persisted.Overwrite(user, new { PasswordLockout = new { FailedAttempts = 2 } });

        user.RecordPasswordFailure(3, Now);

        user.PasswordLockout.FailedAttempts.Should().Be(3);
        user.PasswordLockout.LockedAt.Should().Be(Now);
        user.PasswordLockout.IsLocked.Should().BeTrue();
        user.LoginChallenge.Id.Should().BeNull();
        user.PullDomainEvents().Should().Equal(new PasswordLocked(user.Id));
    }

    [Fact]
    public void RecordPasswordFailureLockedAccountCountsNothing()
    {
        var user = NewPendingUser();
        var lockedAt = Now.AddDays(-1);
        var challengeId = LoginChallengeId.New();
        Persisted.Overwrite(
            user,
            new { PasswordLockout = new { FailedAttempts = 3, LockedAt = lockedAt } }
        );
        user.StartLoginChallenge(challengeId, Now);

        user.RecordPasswordFailure(3, Now);

        user.PasswordLockout.FailedAttempts.Should().Be(3);
        user.PasswordLockout.LockedAt.Should().Be(lockedAt);
        user.LoginChallenge.Id.Should().Be(challengeId);
        user.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void PullDomainEventsOfOneTypeKeepsTheOtherEventsForTheNextPull()
    {
        var user = NewPendingUser();
        user.ChangeType(UserType.Sponsor, Now);
        user.RecordPasswordFailure(1, Now);
        user.ChangeType(UserType.Member, Now);

        var locks = user.PullDomainEvents<PasswordLocked>();

        locks.Should().Equal(new PasswordLocked(user.Id));
        user.PullDomainEvents<PasswordLocked>().Should().BeEmpty();
        user.PullDomainEvents()
            .Should()
            .Equal(new UserTypeChanged(user.Id), new UserTypeChanged(user.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAdministratorDifferentValueChangesItAndStampsTheUpdate(bool isAdmin)
    {
        var user = NewSignInUser();
        Persisted.Overwrite(user, new { IsAdmin = !isAdmin });

        user.SetAdministrator(isAdmin, Now).Value.Should().BeTrue();

        user.IsAdmin.Should().Be(isAdmin);
        user.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAdministratorSameValueChangesNothing(bool isAdmin)
    {
        var user = NewSignInUser();
        Persisted.Overwrite(user, new { IsAdmin = isAdmin });

        user.SetAdministrator(isAdmin, Now).Value.Should().BeFalse();

        user.IsAdmin.Should().Be(isAdmin);
        user.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void SetAdministratorRevokingTheInitialAdministratorIsRefused()
    {
        var administrator = User.CreateInitialAdministrator(
            EmailAddress.FromStored("admin@test.local"),
            "hash",
            Seeded
        );

        var revoked = administrator.SetAdministrator(false, Now);

        revoked.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.UserCannotRemoveInitialAdmin);
        administrator.IsAdmin.Should().BeTrue();
        administrator.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void SetAdministratorGrantingADependentIsRefused()
    {
        var dependent = UserWithStatus(KnownIds.UserStatusTypes.Dependent, passwordHash: null);

        var granted = dependent.SetAdministrator(true, Now);

        granted.ShouldFail(ErrorKind.Validation, DomainErrorCode.UserDependentCannotBeAdmin);
        dependent.IsAdmin.Should().BeFalse();
        dependent.UpdatedAt.Should().BeNull();
        dependent.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void SetAdministratorGrantingAnAccountPendingVerificationIsRefused()
    {
        var pending = NewPendingUser();

        var granted = pending.SetAdministrator(true, Now);

        granted.ShouldFail(
            ErrorKind.Validation,
            DomainErrorCode.UserPendingVerificationCannotBeAdmin
        );
        pending.IsAdmin.Should().BeFalse();
        pending.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void ChangeTypeStoresTheTypeAndStampsTheUpdate()
    {
        var user = NewPendingUser();

        user.ChangeType(UserType.Sponsor, Now);

        user.UserType.Should().Be(UserType.Sponsor);
        user.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void CreateInitialAdministratorBuildsAnActiveAdministratorUnderTheFixedIdentifier()
    {
        var administrator = User.CreateInitialAdministrator(
            EmailAddress.FromStored("admin@test.local"),
            "hash",
            Now
        );

        administrator.Id.Value.Should().Be(KnownIds.Users.InitialAdministrator);
        administrator.FirstName.Should().Be("Administrador");
        administrator.LastName.Should().Be("Código Activo");
        administrator.Email!.Value.Should().Be("admin@test.local");
        administrator.PasswordHash.Should().Be("hash");
        administrator.NationalId.Should().Be(SpanishNationalId.FromDniNumber(0));
        administrator.Gender.Should().Be(Gender.Other);
        administrator.Status.Should().Be(UserStatus.Active);
        administrator.UserType.Should().Be(UserType.Member);
        administrator.ParentId.Should().BeNull();
        administrator.IsAdmin.Should().BeTrue();
        administrator.CreatedAt.Should().Be(Now);
        administrator.CanSignIn.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "hash")]
    [InlineData(" ", "hash")]
    [InlineData("admin@test.local", null)]
    [InlineData("admin@test.local", "")]
    public void CreateInitialAdministratorMissingCredentialThrows(
        string? email,
        string? passwordHash
    )
    {
        var act = () =>
            User.CreateInitialAdministrator(EmailAddress.FromStored(email!), passwordHash!, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResendCooldownsRunOnlyUntilTheCooldownEnds()
    {
        var user = NewPendingUser();
        var cooldown = TimeSpan.FromMinutes(1);
        user.IssueOtp("OTP", Now, TimeSpan.FromMinutes(15));
        user.IssuePasswordResetCode("RESET", Now, TimeSpan.FromMinutes(15));
        user.IssueLoginCode("LOGIN", Now, TimeSpan.FromMinutes(15));

        user.VerificationCode.IsResendCoolingDown(Now.AddSeconds(59), cooldown).Should().BeTrue();
        user.VerificationCode.IsResendCoolingDown(Now.AddSeconds(60), cooldown).Should().BeFalse();
        user.PasswordResetCode.IsResendCoolingDown(Now.AddSeconds(59), cooldown).Should().BeTrue();
        user.PasswordResetCode.IsResendCoolingDown(Now.AddSeconds(60), cooldown).Should().BeFalse();
        user.LoginCode.IsResendCoolingDown(Now.AddSeconds(59), cooldown).Should().BeTrue();
        user.LoginCode.IsResendCoolingDown(Now.AddSeconds(60), cooldown).Should().BeFalse();
    }

    [Fact]
    public void ResendCooldownsNothingSentNeverCoolDown()
    {
        var user = NewPendingUser();
        var cooldown = TimeSpan.FromMinutes(1);
        Persisted.Overwrite(user, new { VerificationCode = default(OneTimeCode) });

        user.VerificationCode.IsResendCoolingDown(Now, cooldown).Should().BeFalse();
        user.PasswordResetCode.IsResendCoolingDown(Now, cooldown).Should().BeFalse();
        user.LoginCode.IsResendCoolingDown(Now, cooldown).Should().BeFalse();
    }

    [Fact]
    public void AssignPasswordStoresTheHash()
    {
        var user = NewPendingUser();

        user.AssignPassword("hash");

        user.PasswordHash.Should().Be("hash");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AssignPasswordBlankHashThrowsAndKeepsNoPassword(string? passwordHash)
    {
        var user = NewPendingUser();

        var act = () => user.AssignPassword(passwordHash!);

        act.Should().Throw<ArgumentException>();
        user.PasswordHash.Should().BeNull();
    }

    [Fact]
    public void ForgetOtpDeliveryKeepsTheCodeButAllowsAnImmediateResend()
    {
        var user = NewPendingUser();
        user.IssueOtp("OTP", Now, TimeSpan.FromMinutes(15));

        user.ForgetOtpDelivery();

        user.VerificationCode.LastSentAt.Should().BeNull();
        user.VerificationCode.Hash.Should().Be("OTP");
        user.VerificationCode.ExpiresAt.Should().Be(Now.AddMinutes(15));
        user.VerificationCode.IsResendCoolingDown(Now, TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void DisableAuthenticatorForgetsTheFailuresWithoutLiftingALock()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(
            user,
            new { TwoFactorLockout = new { FailedAttempts = 2, LockedUntil = Now.AddMinutes(5) } }
        );

        user.DisableAuthenticator(Now);

        user.TwoFactorLockout.FailedAttempts.Should().Be(0);
        user.TwoFactorLockout.IsLocked(Now).Should().BeTrue();
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void HasOpenLoginChallengeOpenChallengeExpiresAfterItsLifetime(
        int minutesLater,
        bool open
    )
    {
        var user = NewSignInUser();
        var challengeId = LoginChallengeId.New();
        user.StartLoginChallenge(challengeId, Now);

        user.LoginChallenge.StartedAt.Should().Be(Now);
        user.HasOpenLoginChallenge(
                challengeId,
                Now.AddMinutes(minutesLater),
                TimeSpan.FromMinutes(10)
            )
            .Should()
            .Be(open);
        user.HasOpenLoginChallenge(LoginChallengeId.New(), Now, TimeSpan.FromMinutes(10))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void HasOpenLoginChallengeAccountThatMayNoLongerSignInClosesIt()
    {
        var blocked = NewSignInUser();
        var passwordLocked = NewSignInUser();
        var challengeId = LoginChallengeId.New();
        blocked.StartLoginChallenge(challengeId, Now);
        passwordLocked.StartLoginChallenge(challengeId, Now);
        Persisted.Overwrite(blocked, new { Status = UserStatus.Blocked });
        Persisted.Overwrite(passwordLocked, new { PasswordLockout = new { LockedAt = Now } });

        blocked
            .HasOpenLoginChallenge(challengeId, Now, TimeSpan.FromMinutes(10))
            .Should()
            .BeFalse();
        passwordLocked
            .HasOpenLoginChallenge(challengeId, Now, TimeSpan.FromMinutes(10))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ClearLoginChallengeForgetsTheChallengeAndWhenItOpened()
    {
        var user = NewSignInUser();
        var challengeId = LoginChallengeId.New();
        user.StartLoginChallenge(challengeId, Now);

        user.ClearLoginChallenge();

        user.LoginChallenge.Id.Should().BeNull();
        user.LoginChallenge.StartedAt.Should().BeNull();
        user.HasOpenLoginChallenge(challengeId, Now, TimeSpan.FromMinutes(10)).Should().BeFalse();
    }

    [Fact]
    public void StartLoginChallengeOnlyTheOpenChallengeMatches()
    {
        var user = NewPendingUser();
        var challengeId = LoginChallengeId.New();

        user.LoginChallenge.Matches(challengeId).Should().BeFalse();

        user.StartLoginChallenge(challengeId, Now);

        user.LoginChallenge.Matches(challengeId).Should().BeTrue();
        user.LoginChallenge.Matches(LoginChallengeId.New()).Should().BeFalse();
    }

    [Fact]
    public void SessionStartExpiresOnceTheLifetimeElapses()
    {
        var userId = Guid.NewGuid();

        var session = UserSession.Start(UserId.From(userId), Now, TimeSpan.FromHours(8));

        session.Id.Value.Should().NotBeEmpty();
        session.UserId.Value.Should().Be(userId);
        session.CreatedAt.Should().Be(Now);
        session.ExpiresAt.Should().Be(Now.AddHours(8));
    }
}
