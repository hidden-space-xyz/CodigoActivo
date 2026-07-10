using AwesomeAssertions;
using CodigoActivo.Domain.Entities;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class UserEntityTests
{
    private static User NewPendingUser() =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@test.local",
            BirthDate = new DateOnly(1990, 1, 1),
            UserStatusTypeId = Guid.NewGuid(),
            OtpCodeHash = "ABCDEF",
            OtpExpiresAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            OtpLastSentAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    [Fact]
    public void Verify_PendingUser_ActivatesAccountAndClearsOtp()
    {
        var activeStatusId = Guid.NewGuid();
        var user = NewPendingUser();
        var before = DateTimeOffset.UtcNow;

        user.Verify(activeStatusId);

        var after = DateTimeOffset.UtcNow;
        user.UserStatusTypeId.Should().Be(activeStatusId);
        user.OtpCodeHash.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        user.OtpLastSentAt.Should().BeNull();
        user.UpdatedAt.Should().NotBeNull();
        user.UpdatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Verify_PendingUser_LeavesLastLoginUntouched()
    {
        var user = NewPendingUser();
        user.LastLoginAt = null;

        user.Verify(Guid.NewGuid());

        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void IssueOtp_HashAndLifetime_StoresHashAndTimestamps()
    {
        var user = NewPendingUser();
        var now = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

        user.IssueOtp("NEWHASH", now, TimeSpan.FromMinutes(15));

        user.OtpCodeHash.Should().Be("NEWHASH");
        user.OtpExpiresAt.Should().Be(now.AddMinutes(15));
        user.OtpLastSentAt.Should().Be(now);
    }

    [Fact]
    public void RegisterLogin_PendingUser_StampsCurrentLastLoginTime()
    {
        var user = NewPendingUser();
        user.LastLoginAt = null;
        var before = DateTimeOffset.UtcNow;

        user.RegisterLogin();

        var after = DateTimeOffset.UtcNow;
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void RegisterLogin_PendingUser_DoesNotChangeStatusOrOtp()
    {
        var user = NewPendingUser();
        var status = user.UserStatusTypeId;
        var otpHash = user.OtpCodeHash;

        user.RegisterLogin();

        user.UserStatusTypeId.Should().Be(status);
        user.OtpCodeHash.Should().Be(otpHash);
        user.UpdatedAt.Should().BeNull();
    }
}
