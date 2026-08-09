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
}
