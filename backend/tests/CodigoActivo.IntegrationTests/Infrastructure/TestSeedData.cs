using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed record TestCredentials(string Identifier, string Password);

public static class TestSeedData
{
    public const string Password = "Str0ngPass!23";
    public const string PasswordHash = FakePasswordHasher.Prefix + Password;

    public static class Users
    {
        public static readonly Guid AdminId = KnownIds.Users.InitialAdministrator;
        public static readonly Guid MemberId = new("22222222-2222-2222-2222-222222222222");
        public static readonly Guid MemberChildId = new("33333333-3333-3333-3333-333333333333");
        public static readonly Guid PendingId = new("44444444-4444-4444-4444-444444444444");
        public static readonly Guid BlockedId = new("55555555-5555-5555-5555-555555555555");
    }

    public const string AdminEmail = "admin@codigoactivo.test";
    public const string MemberEmail = "member@codigoactivo.test";
    public const string PendingEmail = "pending@codigoactivo.test";
    public const string BlockedEmail = "blocked@codigoactivo.test";

    public const string AdminNationalId = "11111111H";
    public const string MemberNationalId = "22222222J";
    public const string PendingNationalId = "33333333P";
    public const string BlockedNationalId = "44444444A";

    public static readonly TestCredentials AdminCredentials = new(AdminEmail, Password);
    public static readonly TestCredentials MemberCredentials = new(MemberEmail, Password);
    public static readonly TestCredentials PendingCredentials = new(PendingEmail, Password);
    public static readonly TestCredentials BlockedCredentials = new(BlockedEmail, Password);

    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] ValidPng()
    {
        return Convert.FromHexString(
            "89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4"
        );
    }

    public static async Task SeedUsersAsync(
        CodigoActivoDbContext db,
        CancellationToken ct = default
    )
    {
        var admin = Persisted.As<User>(
            new
            {
                Id = Users.AdminId,
                FirstName = "Ada",
                LastName = "Admin",
                Email = AdminEmail,
                Phone = "+34600000001",
                PasswordHash = PasswordHash,
                NationalId = AdminNationalId,
                Gender = Gender.Female,
                Status = UserStatus.Active,
                UserType = UserType.Member,
                IsAdmin = true,
                CreatedAt = SeededAt,
            }
        );

        var member = Persisted.As<User>(
            new
            {
                Id = Users.MemberId,
                FirstName = "Marta",
                LastName = "Miembro",
                Email = MemberEmail,
                Phone = "+34600000002",
                PasswordHash = PasswordHash,
                NationalId = MemberNationalId,
                PromotionalConsent = true,
                Gender = Gender.Female,
                Status = UserStatus.Active,
                UserType = UserType.Member,
                CreatedAt = SeededAt,
            }
        );

        var child = Persisted.As<User>(
            new
            {
                Id = Users.MemberChildId,
                FirstName = "Mateo",
                LastName = "Miembro",
                BirthDate = new DateOnly(2015, 5, 5),
                Gender = Gender.Male,
                ParentId = Users.MemberId,
                Status = UserStatus.Dependent,
                UserType = UserType.Participant,
                CreatedAt = SeededAt,
            }
        );

        var pending = Persisted.As<User>(
            new
            {
                Id = Users.PendingId,
                FirstName = "Pedro",
                LastName = "Pendiente",
                Email = PendingEmail,
                Phone = "+34600000003",
                PasswordHash = PasswordHash,
                NationalId = PendingNationalId,
                Gender = Gender.Male,
                Status = UserStatus.Pending,
                UserType = UserType.Member,
                CreatedAt = SeededAt,
            }
        );

        var blocked = Persisted.As<User>(
            new
            {
                Id = Users.BlockedId,
                FirstName = "Bruno",
                LastName = "Bloqueado",
                Email = BlockedEmail,
                Phone = "+34600000004",
                PasswordHash = PasswordHash,
                NationalId = BlockedNationalId,
                Gender = Gender.Other,
                Status = UserStatus.Blocked,
                UserType = UserType.Member,
                CreatedAt = SeededAt,
            }
        );

        db.Users.AddRange(admin, member, child, pending, blocked);

        await db.SaveChangesAsync(ct);
    }
}
