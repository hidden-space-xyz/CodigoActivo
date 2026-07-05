using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Context;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed record TestCredentials(string Identifier, string Password);

public static class TestSeedData
{
    public const string Password = "Str0ngPass!";
    public const string PasswordHash = FakePasswordHasher.Prefix + Password;

    public static class Users
    {
        public static readonly Guid AdminId = new("11111111-1111-1111-1111-111111111111");
        public static readonly Guid MemberId = new("22222222-2222-2222-2222-222222222222");
        public static readonly Guid MemberChildId = new("33333333-3333-3333-3333-333333333333");
        public static readonly Guid PendingId = new("44444444-4444-4444-4444-444444444444");
        public static readonly Guid BlockedId = new("55555555-5555-5555-5555-555555555555");
    }

    public const string AdminEmail = "admin@codigoactivo.test";
    public const string MemberEmail = "member@codigoactivo.test";
    public const string PendingEmail = "pending@codigoactivo.test";
    public const string BlockedEmail = "blocked@codigoactivo.test";

    public static readonly TestCredentials AdminCredentials = new(AdminEmail, Password);
    public static readonly TestCredentials MemberCredentials = new(MemberEmail, Password);
    public static readonly TestCredentials PendingCredentials = new(PendingEmail, Password);
    public static readonly TestCredentials BlockedCredentials = new(BlockedEmail, Password);

    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static async Task SeedUsersAsync(CodigoActivoDbContext db, CancellationToken ct = default)
    {
        var admin = new User
        {
            Id = Users.AdminId,
            FirstName = "Ada",
            LastName = "Admin",
            Email = AdminEmail,
            Phone = "+34600000001",
            PasswordHash = PasswordHash,
            BirthDate = new DateOnly(1985, 3, 12),
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Member,
            IsAdmin = true,
            CreatedAt = SeededAt,
        };

        var member = new User
        {
            Id = Users.MemberId,
            FirstName = "Marta",
            LastName = "Miembro",
            Email = MemberEmail,
            Phone = "+34600000002",
            PasswordHash = PasswordHash,
            BirthDate = new DateOnly(1992, 7, 30),
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Member,
            CreatedAt = SeededAt,
        };

        var child = new User
        {
            Id = Users.MemberChildId,
            FirstName = "Mateo",
            LastName = "Miembro",
            BirthDate = new DateOnly(2015, 5, 5),
            ParentId = Users.MemberId,
            UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = SeededAt,
        };

        var pending = new User
        {
            Id = Users.PendingId,
            FirstName = "Pedro",
            LastName = "Pendiente",
            Email = PendingEmail,
            Phone = "+34600000003",
            PasswordHash = PasswordHash,
            BirthDate = new DateOnly(1990, 1, 1),
            UserStatusTypeId = SeedIds.UserStatusTypes.Pending,
            UserTypeId = SeedIds.UserTypes.Member,
            CreatedAt = SeededAt,
        };

        var blocked = new User
        {
            Id = Users.BlockedId,
            FirstName = "Bruno",
            LastName = "Bloqueado",
            Email = BlockedEmail,
            Phone = "+34600000004",
            PasswordHash = PasswordHash,
            BirthDate = new DateOnly(1988, 9, 9),
            UserStatusTypeId = SeedIds.UserStatusTypes.Blocked,
            UserTypeId = SeedIds.UserTypes.Member,
            CreatedAt = SeededAt,
        };

        db.Users.AddRange(admin, member, child, pending, blocked);

        await db.SaveChangesAsync(ct);
    }
}
