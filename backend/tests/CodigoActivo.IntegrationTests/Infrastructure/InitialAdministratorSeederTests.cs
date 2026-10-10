using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class InitialAdministratorSeederTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.TruncateAllTablesAsync(db);
        await new DatabaseSeeder(db).SeedAsync(TestCancellation.Ct);
    }

    [Fact]
    public async Task SeedAsyncEmptyDatabaseCreatesActiveAdministratorAsFirstUser()
    {
        await using var db = postgres.CreateContext();
        var clock = new TestClock { UtcNow = CreatedAt };
        var seeder = new InitialAdministratorSeeder(db, new FakePasswordHasher(), clock);

        await seeder.SeedAsync("  ADMIN@CodigoActivo.Test  ", TestCancellation.Ct);

        var administrator = await db.Users.AsNoTracking().SingleAsync(TestCancellation.Ct);
        administrator.Id.Value.Should().Be(KnownIds.Users.InitialAdministrator);
        administrator.Email!.Value.Should().Be("admin@codigoactivo.test");
        administrator
            .PasswordHash.Should()
            .MatchRegex(
                "^fake:[A-Za-z0-9+/]{43}=$",
                "the password is 32 random bytes nobody knows"
            );
        administrator.IsAdmin.Should().BeTrue();
        administrator.Status.Should().Be(UserStatus.Active);
        administrator.UserType.Should().Be(UserType.Member);
        administrator.CreatedAt.Should().Be(CreatedAt);
        administrator.BirthDate.Should().BeNull();
        administrator.NationalId!.Value.Should().Be("00000000T");
        administrator.PromotionalConsent.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsyncExistingInitialAdministratorIgnoresMissingBootstrapEmail()
    {
        await using var db = postgres.CreateContext();
        db.Users.Add(NewExistingUser(KnownIds.Users.InitialAdministrator));
        await db.SaveChangesAsync(TestCancellation.Ct);
        var seeder = new InitialAdministratorSeeder(db, new FakePasswordHasher(), new TestClock());

        var act = () => seeder.SeedAsync(null, TestCancellation.Ct);

        await act.Should().NotThrowAsync();
        (await db.Users.CountAsync(TestCancellation.Ct)).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsyncUsersWithoutTheInitialAdministratorFail()
    {
        await using var db = postgres.CreateContext();
        db.Users.Add(NewExistingUser(Guid.NewGuid()));
        await db.SaveChangesAsync(TestCancellation.Ct);
        var seeder = new InitialAdministratorSeeder(db, new FakePasswordHasher(), new TestClock());

        var act = () => seeder.SeedAsync("admin@codigoactivo.test", TestCancellation.Ct);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*initial administrator*");
        (await db.Users.CountAsync(TestCancellation.Ct)).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsyncEmptyDatabaseWithoutEmailFails()
    {
        await using var db = postgres.CreateContext();
        var seeder = new InitialAdministratorSeeder(db, new FakePasswordHasher(), new TestClock());

        var act = () => seeder.SeedAsync(null, TestCancellation.Ct);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*BOOTSTRAP_ADMIN_EMAIL*");
        (await db.Users.CountAsync(TestCancellation.Ct)).Should().Be(0);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static User NewExistingUser(Guid id)
    {
        return Persisted.As<User>(
            new
            {
                Id = id,
                FirstName = "Existing",
                LastName = "User",
                Email = "existing@codigoactivo.test",
                BirthDate = new DateOnly(1990, 1, 1),
                Gender = Gender.Other,
                Status = UserStatus.Active,
                UserType = UserType.Participant,
                CreatedAt = CreatedAt,
            }
        );
    }
}
