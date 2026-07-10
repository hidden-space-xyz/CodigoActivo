using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class QueryExecutorTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Fixed = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly QueryExecutor sut = new();

    private CodigoActivoDbContext NewContext()
    {
        return new CodigoActivoDbContext(
            new DbContextOptionsBuilder<CodigoActivoDbContext>()
                .UseNpgsql(postgres.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options
        );
    }

    public async ValueTask InitializeAsync()
    {
        await using var db = NewContext();
        await TestDatabase.TruncateAllTablesAsync(db);
        await new DatabaseSeeder(db).SeedAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task SeedUsersAsync(params string[] firstNames)
    {
        await using var db = NewContext();
        db.Users.AddRange(
            firstNames.Select(firstName => new User
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = "Paginated",
                BirthDate = new DateOnly(1990, 1, 1),
                UserStatusTypeId = SeedIds.UserStatusTypes.Active,
                UserTypeId = SeedIds.UserTypes.Member,
                CreatedAt = Fixed,
            })
        );

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static IQueryable<User> OrderedUsers(CodigoActivoDbContext db) =>
        db.Users.AsNoTracking().OrderBy(u => u.FirstName);

    [Fact]
    public async Task ToPagedAsync_MiddlePage_ReturnsTotalAndPageSlice()
    {
        await SeedUsersAsync("A", "B", "C", "D", "E");
        await using var db = NewContext();

        var result = await sut.ToPagedAsync(
            OrderedUsers(db),
            page: 2,
            pageSize: 2,
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Select(u => u.FirstName).Should().Equal("C", "D");
    }

    [Fact]
    public async Task ToPagedAsync_LastPartialPage_ReturnsRemainingItems()
    {
        await SeedUsersAsync("A", "B", "C", "D", "E");
        await using var db = NewContext();

        var result = await sut.ToPagedAsync(
            OrderedUsers(db),
            page: 3,
            pageSize: 2,
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(5);
        result.Items.Select(u => u.FirstName).Should().Equal("E");
    }

    [Fact]
    public async Task ToPagedAsync_PageBeyondLast_ReturnsEmptySliceWithTotal()
    {
        await SeedUsersAsync("A", "B");
        await using var db = NewContext();

        var result = await sut.ToPagedAsync(
            OrderedUsers(db),
            page: 5,
            pageSize: 10,
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(2);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ToPagedAsync_EmptySource_ReturnsZeroTotalAndEmptySlice()
    {
        await using var db = NewContext();

        var result = await sut.ToPagedAsync(
            OrderedUsers(db),
            page: 1,
            pageSize: 10,
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ToPagedAsync_PageNumberThatWouldOverflowInt32_ReturnsEmptySliceWithoutThrowing()
    {
        await SeedUsersAsync("A");
        await using var db = NewContext();

        var result = await sut.ToPagedAsync(
            OrderedUsers(db),
            page: int.MaxValue,
            pageSize: 100,
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(1);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ToListAsync_MatchingRows_MaterialisesEveryRow()
    {
        await SeedUsersAsync("A", "B", "C");
        await using var db = NewContext();

        var result = await sut.ToListAsync(OrderedUsers(db), TestContext.Current.CancellationToken);

        result.Select(u => u.FirstName).Should().Equal("A", "B", "C");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_MatchingRows_ReturnsTheFirstInQueryOrder()
    {
        await SeedUsersAsync("B", "A", "C");
        await using var db = NewContext();

        var result = await sut.FirstOrDefaultAsync(
            OrderedUsers(db),
            TestContext.Current.CancellationToken
        );

        result!.FirstName.Should().Be("A");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_NoRows_ReturnsNull()
    {
        await using var db = NewContext();

        var result = await sut.FirstOrDefaultAsync(
            OrderedUsers(db),
            TestContext.Current.CancellationToken
        );

        result.Should().BeNull();
    }
}
