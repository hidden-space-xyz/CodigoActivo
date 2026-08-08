using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;

namespace CodigoActivo.UnitTests.Application.Auth;

internal static class AuthTestData
{
    public static readonly DateOnly AdultBirthDate = new(1990, 1, 1);
    public static readonly DateOnly MinorBirthDate = new(2020, 1, 1);

    public static User NewUser(
        Guid? id = null,
        string? email = "ana@test.com",
        string? passwordHash = "fake:password123",
        Guid? statusId = null,
        string? otpCodeHash = null,
        DateTimeOffset? otpExpiresAt = null,
        DateTimeOffset? otpLastSentAt = null
    )
    {
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "Ruiz",
            Email = email,
            Phone = "+34123456789",
            PasswordHash = passwordHash,
            BirthDate = AdultBirthDate,
            UserStatusTypeId = statusId ?? SeedIds.UserStatusTypes.Active,
            OtpCodeHash = otpCodeHash,
            OtpExpiresAt = otpExpiresAt,
            OtpLastSentAt = otpLastSentAt,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
    }

    public static User NewPendingWithOtp(
        TestClock clock,
        string code = "the-otp-code",
        DateTimeOffset? otpLastSentAt = null
    )
    {
        return NewUser(
            statusId: SeedIds.UserStatusTypes.Pending,
            otpCodeHash: FakePasswordHasher.Prefix + code,
            otpExpiresAt: clock.UtcNow.AddMinutes(5),
            otpLastSentAt: otpLastSentAt ?? clock.UtcNow.AddMinutes(-10)
        );
    }

    public static User NewUserWithResetCode(
        TestClock clock,
        string code = "the-reset-code",
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? lastSentAt = null
    )
    {
        var user = NewUser();
        user.PasswordResetCodeHash = FakePasswordHasher.Prefix + code;
        user.PasswordResetExpiresAt = expiresAt ?? clock.UtcNow.AddMinutes(5);
        user.PasswordResetLastSentAt = lastSentAt ?? clock.UtcNow.AddMinutes(-10);
        return user;
    }

    public static User FindReturns(this IUserRepository users, User? user)
    {
        users.Finds(user);
        return user!;
    }
}
