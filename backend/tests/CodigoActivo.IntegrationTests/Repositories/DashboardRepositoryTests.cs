using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Repositories;

public sealed class DashboardRepositoryTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorId = new("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid ThumbId = new("bbbbbbbb-2222-2222-2222-222222222222");

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

    private CodigoActivoDbContext NewContext()
    {
        return new CodigoActivoDbContext(
            new DbContextOptionsBuilder<CodigoActivoDbContext>()
                .UseNpgsql(postgres.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options
        );
    }

    [Fact]
    public async Task GetCountsAsync_EmptyDatabase_ReturnsZeroForEveryTable()
    {
        await using var ctx = NewContext();
        var repo = new DashboardRepository(ctx);

        var counts = await repo.GetCountsAsync(TestContext.Current.CancellationToken);

        counts.Events.Should().Be(0);
        counts.Activities.Should().Be(0);
        counts.Resources.Should().Be(0);
        counts.Announcements.Should().Be(0);
        counts.Partners.Should().Be(0);
        counts.Users.Should().Be(0);
    }

    [Fact]
    public async Task GetCountsAsync_SeededRows_ReturnsDistinctCountPerTable()
    {
        await using var ctx = NewContext();
        SeedDistinctRowCounts(ctx);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repo = new DashboardRepository(ctx);

        var counts = await repo.GetCountsAsync(TestContext.Current.CancellationToken);

        counts.Events.Should().Be(2);
        counts.Activities.Should().Be(3);
        counts.Resources.Should().Be(1);
        counts.Announcements.Should().Be(4);
        counts.Partners.Should().Be(5);
        counts.Users.Should().Be(6);
    }

    private static void SeedDistinctRowCounts(CodigoActivoDbContext ctx)
    {
        ctx.Users.Add(NewUser(AuthorId, "Author"));
        ctx.Files.Add(
            new FileEntity
            {
                Id = ThumbId,
                Name = "thumb",
                Extension = "png",
                UploadedAt = Fixed,
                UploadedBy = AuthorId,
            }
        );

        for (var i = 0; i < 5; i++)
            ctx.Users.Add(NewUser(Guid.NewGuid(), $"User{i}"));

        var firstEvent = NewEvent("Evento 1");
        ctx.Events.AddRange(firstEvent, NewEvent("Evento 2"));

        for (var i = 0; i < 3; i++)
            ctx.Activities.Add(NewActivity(firstEvent.Id, $"Actividad {i}"));

        ctx.Resources.Add(
            new Resource
            {
                Id = Guid.NewGuid(),
                Title = "Recurso",
                Subtitle = "Sub",
                Description = "{}",
                ResourceTypeId = SeedIds.ResourceTypes.Internal,
                ThumbnailId = ThumbId,
                CreatedAt = Fixed,
                CreatedBy = AuthorId,
            }
        );

        for (var i = 0; i < 4; i++)
        {
            ctx.Announcements.Add(
                new Announcement
                {
                    Id = Guid.NewGuid(),
                    Title = $"Anuncio {i}",
                    Subtitle = "Sub",
                    Description = "{}",
                    ThumbnailId = ThumbId,
                    CreatedAt = Fixed,
                    CreatedBy = AuthorId,
                }
            );
        }

        for (var i = 0; i < 5; i++)
        {
            ctx.Partners.Add(
                new Partner
                {
                    Id = Guid.NewGuid(),
                    Name = $"Socio {i}",
                    Tier = 1,
                    FromDate = new DateOnly(2024, 1, 1),
                    ThumbnailId = ThumbId,
                    CreatedAt = Fixed,
                    CreatedBy = AuthorId,
                }
            );
        }
    }

    private static User NewUser(Guid id, string firstName) =>
        new()
        {
            Id = id,
            FirstName = firstName,
            LastName = "Fixture",
            BirthDate = new DateOnly(1980, 1, 1),
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Member,
            CreatedAt = Fixed,
        };

    private static Event NewEvent(string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = "sub",
            Description = "{}",
            EventStartsAt = new DateOnly(2026, 6, 1),
            EventEndsAt = new DateOnly(2026, 6, 2),
            ThumbnailId = ThumbId,
            CreatedAt = Fixed,
            CreatedBy = AuthorId,
        };

    private static Activity NewActivity(Guid eventId, string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "d",
            Location = "loc",
            ActivityStartsAt = Fixed,
            ActivityEndsAt = Fixed.AddHours(1),
            EventId = eventId,
            ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
            ThumbnailId = ThumbId,
            CreatedAt = Fixed,
            CreatedBy = AuthorId,
        };
}
