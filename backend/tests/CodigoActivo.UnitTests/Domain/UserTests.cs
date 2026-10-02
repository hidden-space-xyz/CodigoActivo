using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly DateTimeOffset Seeded = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<Guid, bool, bool, bool> StatusFlags =>
        new()
        {
            { SeedIds.UserStatusTypes.Pending, false, false, true },
            { SeedIds.UserStatusTypes.Active, false, false, false },
            { SeedIds.UserStatusTypes.Blocked, true, false, false },
            { SeedIds.UserStatusTypes.Dependent, false, true, false },
        };

    public static TheoryData<Guid, string?, bool> SignInStates =>
        new()
        {
            { SeedIds.UserStatusTypes.Active, "hash", true },
            { SeedIds.UserStatusTypes.Active, null, false },
            { SeedIds.UserStatusTypes.Pending, "hash", false },
            { SeedIds.UserStatusTypes.Blocked, "hash", false },
            { SeedIds.UserStatusTypes.Dependent, "hash", false },
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
                UserStatusTypeId = statusId,
                CreatedAt = Seeded,
            }
        );
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
                UserStatusTypeId = SeedIds.UserStatusTypes.Pending,
                OtpCodeHash = "ABCDEF",
                OtpExpiresAt = Seeded,
                OtpLastSentAt = Seeded,
                CreatedAt = Seeded,
            }
        );
    }

    [Fact]
    public void VerifyPendingUserActivatesAccountAndClearsOtp()
    {
        var user = NewPendingUser();

        user.Verify(Now);

        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        user.IsPendingVerification.Should().BeFalse();
        user.OtpCodeHash.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        user.OtpLastSentAt.Should().BeNull();
        user.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void VerifyPendingUserLeavesLastLoginUntouched()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { LastLoginAt = (DateTimeOffset?)null });

        user.Verify(Now);

        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void IssueOtpHashAndLifetimeStoresHashAndTimestamps()
    {
        var user = NewPendingUser();

        user.IssueOtp("NEWHASH", Now, TimeSpan.FromMinutes(15));

        user.OtpCodeHash.Should().Be("NEWHASH");
        user.OtpExpiresAt.Should().Be(Now.AddMinutes(15));
        user.OtpLastSentAt.Should().Be(Now);
    }

    [Fact]
    public void ClearOtpUserWithIssuedOtpClearsHashAndTimestamps()
    {
        var user = NewPendingUser();

        user.ClearOtp();

        user.OtpCodeHash.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        user.OtpLastSentAt.Should().BeNull();
    }

    [Fact]
    public void RegisterLoginPendingUserStampsSuppliedLastLoginTime()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { LastLoginAt = (DateTimeOffset?)null });

        user.RegisterLogin(Now);

        user.LastLoginAt.Should().Be(Now);
    }

    [Fact]
    public void RegisterLoginPendingUserDoesNotChangeStatusOrOtp()
    {
        var user = NewPendingUser();
        var status = user.UserStatusTypeId;
        var otpHash = user.OtpCodeHash;

        user.RegisterLogin(Now);

        user.UserStatusTypeId.Should().Be(status);
        user.OtpCodeHash.Should().Be(otpHash);
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

        user.LoginCodeHash.Should().Be("HASH");
        user.LoginCodeExpiresAt.Should().Be(Now.AddMinutes(10));
        user.LoginCodeLastSentAt.Should().Be(Now);
        user.HasRecentLoginCode(Now.AddSeconds(30), TimeSpan.FromSeconds(60)).Should().BeTrue();
        user.HasRecentLoginCode(Now.AddSeconds(61), TimeSpan.FromSeconds(60)).Should().BeFalse();
        user.HasRecentLoginCode(Now.AddMinutes(11), TimeSpan.FromMinutes(20)).Should().BeFalse();
    }

    [Fact]
    public void ClearLoginCodeForgetsHashAndTimestamps()
    {
        var user = NewPendingUser();
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));

        user.ClearLoginCode();

        user.LoginCodeHash.Should().BeNull();
        user.LoginCodeExpiresAt.Should().BeNull();
        user.LoginCodeLastSentAt.Should().BeNull();
        user.HasRecentLoginCode(Now, TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void RecordTwoFactorFailureBelowTheLimitOnlyCounts()
    {
        var user = NewPendingUser();
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));

        user.RecordTwoFactorFailure(Now, 3, TimeSpan.FromMinutes(15)).Should().BeFalse();

        user.TwoFactorFailedAttempts.Should().Be(1);
        user.TwoFactorLockedUntil.Should().BeNull();
        user.IsTwoFactorLocked(Now).Should().BeFalse();
        user.LoginCodeHash.Should().Be("HASH");
    }

    [Fact]
    public void RecordTwoFactorFailureAtTheLimitLocksResetsCounterAndDiscardsTheCode()
    {
        var user = NewPendingUser();
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));
        user.StartLoginChallenge(Guid.NewGuid());
        Persisted.Overwrite(user, new { TwoFactorFailedAttempts = 2 });

        user.RecordTwoFactorFailure(Now, 3, TimeSpan.FromMinutes(15)).Should().BeTrue();

        user.TwoFactorFailedAttempts.Should().Be(0);
        user.TwoFactorLockedUntil.Should().Be(Now.AddMinutes(15));
        user.IsTwoFactorLocked(Now.AddMinutes(14)).Should().BeTrue();
        user.IsTwoFactorLocked(Now.AddMinutes(15)).Should().BeFalse();
        user.LoginCodeHash.Should().BeNull();
        user.LoginChallengeId.Should().BeNull();
    }

    [Fact]
    public void StartLoginChallengeReplacesTheIdentifierOfAnyOpenChallenge()
    {
        var user = NewPendingUser();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        user.StartLoginChallenge(first);
        user.LoginChallengeId.Should().Be(first);

        user.StartLoginChallenge(second);
        user.LoginChallengeId.Should().Be(second);

        user.ClearLoginChallenge();
        user.LoginChallengeId.Should().BeNull();
    }

    [Fact]
    public void RecordTwoFactorFailureBelowTheLimitKeepsTheOpenChallenge()
    {
        var user = NewPendingUser();
        var challengeId = Guid.NewGuid();
        user.StartLoginChallenge(challengeId);

        user.RecordTwoFactorFailure(Now, 3, TimeSpan.FromMinutes(15)).Should().BeFalse();

        user.LoginChallengeId.Should().Be(challengeId);
    }

    [Fact]
    public void ResetPasswordClosesAnyOpenChallenge()
    {
        var user = NewPendingUser();
        user.StartLoginChallenge(Guid.NewGuid());

        user.ResetPassword("new-hash", Now);

        user.LoginChallengeId.Should().BeNull();
    }

    [Fact]
    public void CompleteTwoFactorLoginClearsChallengeStateAndStampsLogin()
    {
        var user = NewPendingUser();
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));
        user.StartLoginChallenge(Guid.NewGuid());
        Persisted.Overwrite(
            user,
            new { TwoFactorFailedAttempts = 2, TwoFactorLockedUntil = Now.AddMinutes(-1) }
        );

        user.CompleteTwoFactorLogin(Now);

        user.LoginCodeHash.Should().BeNull();
        user.LoginChallengeId.Should().BeNull();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.TwoFactorLockedUntil.Should().BeNull();
        user.LastLoginAt.Should().Be(Now);
    }

    [Fact]
    public void BeginAuthenticatorSetupStoresPendingKeyUntilItExpires()
    {
        var user = NewPendingUser();

        user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));

        user.PendingAuthenticatorKey.Should().Be("PENDING");
        user.PendingAuthenticatorExpiresAt.Should().Be(Now.AddMinutes(15));
        user.HasPendingAuthenticator(Now.AddMinutes(14)).Should().BeTrue();
        user.HasPendingAuthenticator(Now.AddMinutes(15)).Should().BeFalse();
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
        user.AuthenticatorKey.Should().Be("PENDING");
        user.AuthenticatorLastUsedStep.Should().Be(42);
        user.PendingAuthenticatorKey.Should().BeNull();
        user.PendingAuthenticatorExpiresAt.Should().BeNull();
        user.LoginCodeHash.Should().BeNull();
        user.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void UseEmailTwoFactorForgetsActiveAndPendingAuthenticators()
    {
        var user = NewPendingUser();
        user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));
        user.EnableAuthenticator(42, Now);
        user.BeginAuthenticatorSetup("ANOTHER", Now, TimeSpan.FromMinutes(15));

        user.UseEmailTwoFactor(Now.AddHours(1));

        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.AuthenticatorKey.Should().BeNull();
        user.AuthenticatorLastUsedStep.Should().BeNull();
        user.PendingAuthenticatorKey.Should().BeNull();
        user.PendingAuthenticatorExpiresAt.Should().BeNull();
        user.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void ClearPasswordFailuresForgetsTheCountWithoutLiftingAnExistingLock()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { PasswordFailedAttempts = 5, PasswordLockedAt = Now });

        user.ClearPasswordFailures();

        user.PasswordFailedAttempts.Should().Be(0);
        user.IsPasswordLocked().Should().BeTrue();
    }

    [Fact]
    public void ResetPasswordClearsTheLockAndTheFailureCount()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { PasswordFailedAttempts = 5, PasswordLockedAt = Now });
        user.IssuePasswordResetCode("HASH", Now, TimeSpan.FromMinutes(15));

        user.ResetPassword("new-hash", Now.AddMinutes(1));

        user.PasswordHash.Should().Be("new-hash");
        user.PasswordFailedAttempts.Should().Be(0);
        user.PasswordLockedAt.Should().BeNull();
        user.IsPasswordLocked().Should().BeFalse();
        user.PasswordResetCodeHash.Should().BeNull();
        user.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void ResetTwoFactorLeavesAPasswordLockInPlace()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { PasswordFailedAttempts = 5, PasswordLockedAt = Now });

        user.ResetTwoFactor(Now);

        user.IsPasswordLocked().Should().BeTrue();
    }

    [Fact]
    public void ResetTwoFactorReturnsEverySecondFactorFieldToItsDefault()
    {
        var user = NewPendingUser();
        user.BeginAuthenticatorSetup("PENDING", Now, TimeSpan.FromMinutes(15));
        user.EnableAuthenticator(42, Now);
        user.IssueLoginCode("HASH", Now, TimeSpan.FromMinutes(10));
        Persisted.Overwrite(
            user,
            new { TwoFactorFailedAttempts = 4, TwoFactorLockedUntil = Now.AddMinutes(10) }
        );

        user.ResetTwoFactor(Now);

        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.AuthenticatorKey.Should().BeNull();
        user.LoginCodeHash.Should().BeNull();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.TwoFactorLockedUntil.Should().BeNull();
        user.IsTwoFactorLocked(Now).Should().BeFalse();
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
    public void UsableOtpCodeHashIssuedCodeIsUsableUntilItExpires(int minutesLater, bool usable)
    {
        var user = NewPendingUser();
        user.IssueOtp("OTP", Now, TimeSpan.FromMinutes(10));

        user.UsableOtpCodeHash(Now.AddMinutes(minutesLater)).Should().Be(usable ? "OTP" : null);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void UsableLoginCodeHashIssuedCodeIsUsableUntilItExpires(int minutesLater, bool usable)
    {
        var user = NewPendingUser();
        user.IssueLoginCode("LOGIN", Now, TimeSpan.FromMinutes(10));

        user.UsableLoginCodeHash(Now.AddMinutes(minutesLater)).Should().Be(usable ? "LOGIN" : null);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void UsablePasswordResetCodeHashIssuedCodeIsUsableUntilItExpires(
        int minutesLater,
        bool usable
    )
    {
        var user = NewPendingUser();
        user.IssuePasswordResetCode("RESET", Now, TimeSpan.FromMinutes(10));

        user.UsablePasswordResetCodeHash(Now.AddMinutes(minutesLater))
            .Should()
            .Be(usable ? "RESET" : null);
    }

    [Fact]
    public void UsableCodeHashesNoCodeIssuedReturnNothing()
    {
        var user = NewPendingUser();
        user.ClearOtp();

        user.UsableOtpCodeHash(Now).Should().BeNull();
        user.UsableLoginCodeHash(Now).Should().BeNull();
        user.UsablePasswordResetCodeHash(Now).Should().BeNull();
    }

    [Theory]
    [InlineData(null, 7L, true)]
    [InlineData(6L, 7L, true)]
    [InlineData(7L, 7L, false)]
    [InlineData(8L, 7L, false)]
    public void AcceptsAuthenticatorStepOnlyAStepNewerThanTheLastUsedIsAccepted(
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

        user.AcceptsAuthenticatorStep(step).Should().Be(expected);
    }

    [Fact]
    public void RecordAuthenticatorStepRemembersTheStepAgainstReplays()
    {
        var user = NewPendingUser();

        user.RecordAuthenticatorStep(42);

        user.AuthenticatorLastUsedStep.Should().Be(42);
        user.AcceptsAuthenticatorStep(42).Should().BeFalse();
        user.AcceptsAuthenticatorStep(43).Should().BeTrue();
    }

    [Fact]
    public void RecordPasswordFailureBelowTheLimitOnlyCounts()
    {
        var user = NewPendingUser();
        var challengeId = Guid.NewGuid();
        user.StartLoginChallenge(challengeId);

        user.RecordPasswordFailure(3, Now).Should().BeFalse();

        user.PasswordFailedAttempts.Should().Be(1);
        user.PasswordLockedAt.Should().BeNull();
        user.IsPasswordLocked().Should().BeFalse();
        user.LoginChallengeId.Should().Be(challengeId);
    }

    [Fact]
    public void RecordPasswordFailureReachingTheLimitLocksAndClosesTheChallenge()
    {
        var user = NewPendingUser();
        user.StartLoginChallenge(Guid.NewGuid());
        Persisted.Overwrite(user, new { PasswordFailedAttempts = 2 });

        user.RecordPasswordFailure(3, Now).Should().BeTrue();

        user.PasswordFailedAttempts.Should().Be(3);
        user.PasswordLockedAt.Should().Be(Now);
        user.IsPasswordLocked().Should().BeTrue();
        user.LoginChallengeId.Should().BeNull();
    }

    [Fact]
    public void RecordPasswordFailureLockedAccountCountsNothing()
    {
        var user = NewPendingUser();
        var lockedAt = Now.AddDays(-1);
        var challengeId = Guid.NewGuid();
        Persisted.Overwrite(user, new { PasswordFailedAttempts = 3, PasswordLockedAt = lockedAt });
        user.StartLoginChallenge(challengeId);

        user.RecordPasswordFailure(3, Now).Should().BeFalse();

        user.PasswordFailedAttempts.Should().Be(3);
        user.PasswordLockedAt.Should().Be(lockedAt);
        user.LoginChallengeId.Should().Be(challengeId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAdministratorDifferentValueChangesItAndStampsTheUpdate(bool isAdmin)
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { IsAdmin = !isAdmin });

        user.SetAdministrator(isAdmin, Now).Should().BeTrue();

        user.IsAdmin.Should().Be(isAdmin);
        user.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAdministratorSameValueChangesNothing(bool isAdmin)
    {
        var user = NewPendingUser();
        Persisted.Overwrite(user, new { IsAdmin = isAdmin });

        user.SetAdministrator(isAdmin, Now).Should().BeFalse();

        user.IsAdmin.Should().Be(isAdmin);
        user.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void ChangeTypeStoresTheTypeAndStampsTheUpdate()
    {
        var user = NewPendingUser();

        user.ChangeType(SeedIds.UserTypes.Sponsor, Now);

        user.UserTypeId.Should().Be(SeedIds.UserTypes.Sponsor);
        user.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void CreateInitialAdministratorBuildsAnActiveAdministratorUnderTheFixedIdentifier()
    {
        var administrator = User.CreateInitialAdministrator("admin@test.local", "hash", Now);

        administrator.Id.Should().Be(SeedIds.Users.InitialAdministrator);
        administrator.FirstName.Should().Be("Administrador");
        administrator.LastName.Should().Be("Código Activo");
        administrator.Email.Should().Be("admin@test.local");
        administrator.PasswordHash.Should().Be("hash");
        administrator.NationalId.Should().Be(SpanishNationalId.FromDniNumber(0));
        administrator.Gender.Should().Be(Gender.Other);
        administrator.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        administrator.UserTypeId.Should().Be(SeedIds.UserTypes.Member);
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
        var act = () => User.CreateInitialAdministrator(email!, passwordHash!, Now);

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

        user.IsOtpResendCoolingDown(Now.AddSeconds(59), cooldown).Should().BeTrue();
        user.IsOtpResendCoolingDown(Now.AddSeconds(60), cooldown).Should().BeFalse();
        user.IsPasswordResetResendCoolingDown(Now.AddSeconds(59), cooldown).Should().BeTrue();
        user.IsPasswordResetResendCoolingDown(Now.AddSeconds(60), cooldown).Should().BeFalse();
        user.IsLoginCodeResendCoolingDown(Now.AddSeconds(59), cooldown).Should().BeTrue();
        user.IsLoginCodeResendCoolingDown(Now.AddSeconds(60), cooldown).Should().BeFalse();
    }

    [Fact]
    public void ResendCooldownsNothingSentNeverCoolDown()
    {
        var user = NewPendingUser();
        var cooldown = TimeSpan.FromMinutes(1);
        user.ClearOtp();

        user.IsOtpResendCoolingDown(Now, cooldown).Should().BeFalse();
        user.IsPasswordResetResendCoolingDown(Now, cooldown).Should().BeFalse();
        user.IsLoginCodeResendCoolingDown(Now, cooldown).Should().BeFalse();
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

        user.OtpLastSentAt.Should().BeNull();
        user.OtpCodeHash.Should().Be("OTP");
        user.OtpExpiresAt.Should().Be(Now.AddMinutes(15));
        user.IsOtpResendCoolingDown(Now, TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void ClearTwoFactorFailuresForgetsTheCountWithoutLiftingALock()
    {
        var user = NewPendingUser();
        Persisted.Overwrite(
            user,
            new { TwoFactorFailedAttempts = 2, TwoFactorLockedUntil = Now.AddMinutes(5) }
        );

        user.ClearTwoFactorFailures();

        user.TwoFactorFailedAttempts.Should().Be(0);
        user.IsTwoFactorLocked(Now).Should().BeTrue();
    }

    [Fact]
    public void HasLoginChallengeOnlyMatchesTheOpenChallenge()
    {
        var user = NewPendingUser();
        var challengeId = Guid.NewGuid();

        user.HasLoginChallenge(challengeId).Should().BeFalse();

        user.StartLoginChallenge(challengeId);

        user.HasLoginChallenge(challengeId).Should().BeTrue();
        user.HasLoginChallenge(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void SessionStartExpiresOnceTheLifetimeElapses()
    {
        var userId = Guid.NewGuid();

        var session = UserSession.Start(userId, Now, TimeSpan.FromHours(8));

        session.Id.Should().NotBeEmpty();
        session.UserId.Should().Be(userId);
        session.CreatedAt.Should().Be(Now);
        session.ExpiresAt.Should().Be(Now.AddHours(8));
    }
}
