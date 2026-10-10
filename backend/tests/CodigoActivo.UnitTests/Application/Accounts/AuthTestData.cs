using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Gender = CodigoActivo.Application.Users.Contracts.Gender;
using TwoFactorMethod = CodigoActivo.Application.Accounts.Contracts.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Accounts;

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
        return Persisted.As<User>(
            new
            {
                Id = id ?? Guid.NewGuid(),
                FirstName = "Ana",
                LastName = "Ruiz",
                Email = email,
                Phone = "+34123456789",
                PasswordHash = passwordHash,
                NationalId = "12345678Z",
                Status = CatalogIds.UserStatuses.ValueOf(
                    statusId ?? KnownIds.UserStatusTypes.Active
                ),
                OtpCodeHash = otpCodeHash,
                OtpExpiresAt = otpExpiresAt,
                OtpLastSentAt = otpLastSentAt,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }
        );
    }

    public static UserRow NewUserRow(
        Guid? id = null,
        string? email = "ana@test.com",
        string? passwordHash = "fake:password123",
        Guid? statusId = null,
        TwoFactorMethod twoFactorMethod = TwoFactorMethod.Email,
        Guid? loginChallengeId = null,
        DateTimeOffset? passwordLockedAt = null,
        bool isAdmin = false
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
            NationalId = "12345678Z",
            UserStatusTypeId = statusId ?? KnownIds.UserStatusTypes.Active,
            IsAdmin = isAdmin,
            TwoFactorMethod = twoFactorMethod,
            LoginChallengeId = loginChallengeId,
            PasswordLockedAt = passwordLockedAt,
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
            statusId: KnownIds.UserStatusTypes.Pending,
            otpCodeHash: FakeOneTimeCodeHasher.Prefix + code,
            otpExpiresAt: clock.UtcNow.AddMinutes(5),
            otpLastSentAt: otpLastSentAt ?? clock.UtcNow.AddMinutes(-10)
        );
    }

    public static User NewUserWithLoginCode(
        TestClock clock,
        string code = "123456",
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? lastSentAt = null
    )
    {
        var user = NewUser();
        Persisted.Overwrite(
            user,
            new
            {
                LoginCodeHash = FakeOneTimeCodeHasher.Prefix + code,
                LoginCodeExpiresAt = expiresAt ?? clock.UtcNow.AddMinutes(5),
                LoginCodeLastSentAt = lastSentAt ?? clock.UtcNow.AddMinutes(-2),
            }
        );
        return user;
    }

    public static User NewUserWithAuthenticator(string secret, long? lastUsedStep = null)
    {
        var user = NewUser();
        Persisted.Overwrite(
            user,
            new
            {
                TwoFactorMethod = TwoFactorMethod.Authenticator,
                AuthenticatorKey = FakeSecretProtector.Prefix + secret,
                AuthenticatorLastUsedStep = lastUsedStep,
            }
        );
        return user;
    }

    public static User NewUserWithResetCode(
        TestClock clock,
        string code = "the-reset-code",
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? lastSentAt = null
    )
    {
        var user = NewUser();
        Persisted.Overwrite(
            user,
            new
            {
                PasswordResetCodeHash = FakeOneTimeCodeHasher.Prefix + code,
                PasswordResetExpiresAt = expiresAt ?? clock.UtcNow.AddMinutes(5),
                PasswordResetLastSentAt = lastSentAt ?? clock.UtcNow.AddMinutes(-10),
            }
        );
        return user;
    }

    public static User FindReturns(this IUserRepository users, User? user)
    {
        users.Finds(user);
        return user!;
    }
}
