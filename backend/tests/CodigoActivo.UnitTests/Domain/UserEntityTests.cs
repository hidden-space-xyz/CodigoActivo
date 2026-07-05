using CodigoActivo.Domain.Entities;
using AwesomeAssertions;
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
            OtpCode = Guid.NewGuid(),
            OtpExpiresAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    [Fact]
    public void Verify_activates_the_account_and_clears_the_otp()
    {
        var activeStatusId = Guid.NewGuid();
        var user = NewPendingUser();
        var before = DateTimeOffset.UtcNow;

        user.Verify(activeStatusId);

        var after = DateTimeOffset.UtcNow;
        user.UserStatusTypeId.Should().Be(activeStatusId);
        user.OtpCode.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        user.UpdatedAt.Should().NotBeNull();
        user.UpdatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Verify_leaves_the_last_login_untouched()
    {
        var user = NewPendingUser();
        user.LastLoginAt = null;

        user.Verify(Guid.NewGuid());

        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void RegisterLogin_stamps_the_current_last_login_time()
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
    public void RegisterLogin_does_not_change_status_or_otp()
    {
        var user = NewPendingUser();
        var status = user.UserStatusTypeId;
        var otp = user.OtpCode;

        user.RegisterLogin();

        user.UserStatusTypeId.Should().Be(status);
        user.OtpCode.Should().Be(otp);
        user.UpdatedAt.Should().BeNull();
    }
}
