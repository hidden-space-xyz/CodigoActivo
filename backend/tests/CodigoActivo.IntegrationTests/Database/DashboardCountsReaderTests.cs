using System.Globalization;
using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Reports;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class DashboardCountsReaderTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorId = new("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid ThumbId = new("bbbbbbbb-2222-2222-2222-222222222222");

    public async ValueTask InitializeAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.TruncateAllTablesAsync(db);
        await new DatabaseSeeder(db).SeedAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetCountsAsyncEmptyDatabaseReturnsZeroForEveryTable()
    {
        await using var ctx = postgres.CreateContext();
        await using var readContext = postgres.CreateReadContext();
        var reader = new DashboardCountsReader(readContext);

        var counts = await reader.GetCountsAsync(Ct);

        counts.Events.Should().Be(0);
        counts.Activities.Should().Be(0);
        counts.Resources.Should().Be(0);
        counts.News.Should().Be(0);
        counts.Partners.Should().Be(0);
        counts.Users.Should().Be(0);
    }

    [Fact]
    public async Task GetCountsAsyncSeededRowsReturnsDistinctCountPerTable()
    {
        await using var ctx = postgres.CreateContext();
        SeedDistinctRowCounts(ctx);
        await ctx.SaveChangesAsync(Ct);
        await using var readContext = postgres.CreateReadContext();
        var reader = new DashboardCountsReader(readContext);

        var counts = await reader.GetCountsAsync(Ct);

        counts.Events.Should().Be(2);
        counts.Activities.Should().Be(3);
        counts.Resources.Should().Be(1);
        counts.News.Should().Be(4);
        counts.Partners.Should().Be(5);
        counts.Users.Should().Be(6);
    }

    private static void SeedDistinctRowCounts(CodigoActivoDbContext ctx)
    {
        ctx.Users.Add(NewUser(AuthorId, "Author"));
        ctx.Files.Add(
            Persisted.As<StoredFile>(
                new
                {
                    Id = ThumbId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = Fixed,
                    UploadedBy = AuthorId,
                }
            )
        );
        AddUsers(ctx);

        var firstEvent = NewEvent("Evento 1");
        ctx.Events.AddRange(firstEvent, NewEvent("Evento 2"));
        AddActivities(ctx, firstEvent.Id.Value);

        ctx.Resources.Add(
            Persisted.As<Resource>(
                new
                {
                    Id = Guid.NewGuid(),
                    Title = "Recurso",
                    Subtitle = "Sub",
                    Description = "{}",
                    ResourceType = ResourceType.Internal,
                    ThumbnailId = ThumbId,
                    CreatedAt = Fixed,
                    CreatedBy = AuthorId,
                }
            )
        );
        AddNews(ctx);
        AddPartners(ctx);
    }

    private static void AddUsers(CodigoActivoDbContext ctx)
    {
        ctx.Users.AddRange(
            Enumerable
                .Range(0, 5)
                .Select(i =>
                    NewUser(Guid.NewGuid(), $"User{i.ToString(CultureInfo.InvariantCulture)}")
                )
        );
    }

    private static void AddActivities(CodigoActivoDbContext ctx, Guid eventId)
    {
        ctx.Activities.AddRange(
            Enumerable
                .Range(0, 3)
                .Select(i =>
                    NewActivity(eventId, $"Actividad {i.ToString(CultureInfo.InvariantCulture)}")
                )
        );
    }

    private static void AddNews(CodigoActivoDbContext ctx)
    {
        ctx.News.AddRange(
            Enumerable
                .Range(0, 4)
                .Select(i =>
                    Persisted.As<NewsItem>(
                        new
                        {
                            Id = Guid.NewGuid(),
                            Title = $"Novedad {i.ToString(CultureInfo.InvariantCulture)}",
                            Subtitle = "Sub",
                            Description = "{}",
                            ThumbnailId = ThumbId,
                            CreatedAt = Fixed,
                            CreatedBy = AuthorId,
                        }
                    )
                )
        );
    }

    private static void AddPartners(CodigoActivoDbContext ctx)
    {
        ctx.Partners.AddRange(
            Enumerable
                .Range(0, 5)
                .Select(i =>
                    Persisted.As<Partner>(
                        new
                        {
                            Id = Guid.NewGuid(),
                            Name = $"Socio {i.ToString(CultureInfo.InvariantCulture)}",
                            Tier = 1,
                            FromDate = new DateOnly(2024, 1, 1),
                            ThumbnailId = ThumbId,
                            CreatedAt = Fixed,
                            CreatedBy = AuthorId,
                        }
                    )
                )
        );
    }

    private static User NewUser(Guid id, string firstName)
    {
        return Persisted.As<User>(
            new
            {
                Id = id,
                FirstName = firstName,
                LastName = "Fixture",
                BirthDate = new DateOnly(1980, 1, 1),
                Status = UserStatus.Active,
                UserType = UserType.Member,
                CreatedAt = Fixed,
            }
        );
    }

    private static Event NewEvent(string title)
    {
        return Persisted.As<Event>(
            new
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
            }
        );
    }

    private static Activity NewActivity(Guid eventId, string title)
    {
        return Persisted.As<Activity>(
            new
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = "d",
                Location = "loc",
                ActivityStartsAt = Fixed,
                ActivityEndsAt = Fixed.AddHours(1),
                EventId = eventId,
                Modality = ActivityModality.Presencial,
                ThumbnailId = ThumbId,
                CreatedAt = Fixed,
                CreatedBy = AuthorId,
            }
        );
    }
}
