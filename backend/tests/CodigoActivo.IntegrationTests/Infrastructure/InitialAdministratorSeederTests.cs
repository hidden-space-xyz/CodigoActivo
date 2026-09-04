using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class InitialAdministratorSeederTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private static readonly DateTimeOffset CreatedAt = new(
        2026,
        9,
        4,
        12,
        0,
        0,
        TimeSpan.Zero
    );

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
        var seeder = new InitialAdministratorSeeder(
            db,
            new FakePasswordHasher(),
            clock,
            NullLogger<InitialAdministratorSeeder>.Instance
        );

        await seeder.SeedAsync(
            "  ADMIN@CodigoActivo.Test  ",
            "bootstrap-password-123",
            TestCancellation.Ct
        );

        var administrator = await db.Users.AsNoTracking().SingleAsync(TestCancellation.Ct);
        administrator.Email.Should().Be("admin@codigoactivo.test");
        administrator.PasswordHash.Should().Be("fake:bootstrap-password-123");
        administrator.IsAdmin.Should().BeTrue();
        administrator.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        administrator.UserTypeId.Should().Be(SeedIds.UserTypes.Member);
        administrator.CreatedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public async Task SeedAsyncExistingUserIgnoresMissingBootstrapCredentials()
    {
        await using var db = postgres.CreateContext();
        db.Users.Add(NewExistingUser());
        await db.SaveChangesAsync(TestCancellation.Ct);
        var seeder = new InitialAdministratorSeeder(
            db,
            new FakePasswordHasher(),
            new TestClock(),
            NullLogger<InitialAdministratorSeeder>.Instance
        );

        var act = () => seeder.SeedAsync(null, null, TestCancellation.Ct);

        await act.Should().NotThrowAsync();
        (await db.Users.CountAsync(TestCancellation.Ct)).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsyncEmptyDatabaseWithoutPasswordFails()
    {
        await using var db = postgres.CreateContext();
        var seeder = new InitialAdministratorSeeder(
            db,
            new FakePasswordHasher(),
            new TestClock(),
            NullLogger<InitialAdministratorSeeder>.Instance
        );

        var act = () =>
            seeder.SeedAsync("admin@codigoactivo.test", null, TestCancellation.Ct);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*BOOTSTRAP_ADMIN_PASSWORD*");
        (await db.Users.CountAsync(TestCancellation.Ct)).Should().Be(0);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static User NewExistingUser()
    {
        return new User
        {
            FirstName = "Existing",
            LastName = "User",
            Email = "existing@codigoactivo.test",
            BirthDate = new DateOnly(1990, 1, 1),
            Gender = Gender.Other,
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = CreatedAt,
        };
    }
}
