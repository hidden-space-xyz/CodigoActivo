using AwesomeAssertions;
using CodigoActivo.Domain.Entities;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class UserEntityTests
{
    private static readonly DateTimeOffset Seeded = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private static User NewPendingUser()
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@test.local",
            BirthDate = new DateOnly(1990, 1, 1),
            UserStatusTypeId = Guid.NewGuid(),
            OtpCodeHash = "ABCDEF",
            OtpExpiresAt = Seeded,
            OtpLastSentAt = Seeded,
            CreatedAt = Seeded,
        };
    }

    [Fact]
    public void VerifyPendingUserActivatesAccountAndClearsOtp()
    {
        var activeStatusId = Guid.NewGuid();
        var user = NewPendingUser();

        user.Verify(activeStatusId, Now);

        user.UserStatusTypeId.Should().Be(activeStatusId);
        user.OtpCodeHash.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        user.OtpLastSentAt.Should().BeNull();
        user.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void VerifyPendingUserLeavesLastLoginUntouched()
    {
        var user = NewPendingUser();
        user.LastLoginAt = null;

        user.Verify(Guid.NewGuid(), Now);

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
        user.LastLoginAt = null;

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
        user.TwoFactorFailedAttempts = 2;

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
        user.TwoFactorFailedAttempts = 2;
        user.TwoFactorLockedUntil = Now.AddMinutes(-1);

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
        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email, "setup does not change the factor yet");
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
        user.PasswordFailedAttempts = 5;
        user.PasswordLockedAt = Now;

        user.ClearPasswordFailures();

        user.PasswordFailedAttempts.Should().Be(0);
        user.IsPasswordLocked().Should().BeTrue();
    }

    [Fact]
    public void ResetPasswordClearsTheLockAndTheFailureCount()
    {
        var user = NewPendingUser();
        user.PasswordFailedAttempts = 5;
        user.PasswordLockedAt = Now;
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
        user.PasswordFailedAttempts = 5;
        user.PasswordLockedAt = Now;

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
        user.TwoFactorFailedAttempts = 4;
        user.TwoFactorLockedUntil = Now.AddMinutes(10);

        user.ResetTwoFactor(Now);

        user.TwoFactorMethod.Should().Be(TwoFactorMethod.Email);
        user.AuthenticatorKey.Should().BeNull();
        user.LoginCodeHash.Should().BeNull();
        user.TwoFactorFailedAttempts.Should().Be(0);
        user.TwoFactorLockedUntil.Should().BeNull();
        user.IsTwoFactorLocked(Now).Should().BeFalse();
    }
}
