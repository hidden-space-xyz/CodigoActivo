using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class CachingBehaviorTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_SecondAnonymousRequest_IsServedFromOutputCache()
    {
        await SeedEventAsync();
        var client = CreateClient();

        using var first = await client.GetAsync("/api/events", Ct);
        using var second = await client.GetAsync("/api/events", Ct);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        first.Headers.Age.Should().BeNull();
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Headers.Age.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_AfterAnonymousListCached_AnonymousListReflectsNewEvent()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync();
        var anonymous = CreateClient();

        using var warm = await anonymous.GetAsync("/api/events", Ct);
        warm.StatusCode.Should().Be(HttpStatusCode.OK);

        var admin = await LoginAsAdminAsync();
        using var created = await admin.PostJsonAsync(
            "/api/events",
            BuildCreateEvent("Evento tras caché", thumbnailId, [categoryId]),
            Ct
        );
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using var after = await anonymous.GetAsync("/api/events", Ct);
        var page = await after.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        page!.Items.Should().Contain(e => e.Title == "Evento tras caché");
    }

    [Fact]
    public async Task Feature_AfterAnonymousListCached_ListShowsExclusiveFeaturedFlags()
    {
        var firstId = await SeedEventAsync(title: "Primero", featured: true);
        var secondId = await SeedEventAsync(title: "Segundo");
        var anonymous = CreateClient();

        using var warm = await anonymous.GetAsync("/api/events", Ct);
        warm.StatusCode.Should().Be(HttpStatusCode.OK);

        var admin = await LoginAsAdminAsync();
        using var featured = await admin.PatchJsonAsync(
            $"/api/events/{secondId}/feature",
            null,
            Ct
        );
        featured.StatusCode.Should().Be(HttpStatusCode.OK);

        using var after = await anonymous.GetAsync("/api/events", Ct);
        var page = await after.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        page!.Items.Single(e => e.Id == firstId).Featured.Should().BeFalse();
        page.Items.Single(e => e.Id == secondId).Featured.Should().BeTrue();
    }

    [Fact]
    public async Task Dashboard_AfterEventCreate_ReflectsNewEventCount()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync();
        var admin = await LoginAsAdminAsync();

        using var before = await admin.GetAsync("/api/reports/dashboard", Ct);
        var beforeCounts = await before.ReadJsonAsync<DashboardSummaryResponse>(Ct);

        using var created = await admin.PostJsonAsync(
            "/api/events",
            BuildCreateEvent("Evento para dashboard", thumbnailId, [categoryId]),
            Ct
        );
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using var after = await admin.GetAsync("/api/reports/dashboard", Ct);
        var afterCounts = await after.ReadJsonAsync<DashboardSummaryResponse>(Ct);
        afterCounts!.Events.Should().Be(beforeCounts!.Events + 1);
    }

    [Fact]
    public async Task UpdateCategoryType_AfterAnonymousListCached_ListShowsRenamedCategory()
    {
        var categoryId = await SeedCategoryTypeAsync("Original");
        await SeedEventAsync(categoryTypeId: categoryId);
        var anonymous = CreateClient();

        using var warm = await anonymous.GetAsync("/api/events", Ct);
        warm.StatusCode.Should().Be(HttpStatusCode.OK);

        var admin = await LoginAsAdminAsync();
        using var renamed = await admin.PutJsonAsync(
            $"/api/events/categoryType/{categoryId}",
            new UpdateEventCategoryTypeRequest("Renombrada", "#112233"),
            Ct
        );
        renamed.StatusCode.Should().Be(HttpStatusCode.OK);

        using var after = await anonymous.GetAsync("/api/events", Ct);
        var page = await after.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        page!
            .Items.Single()
            .Categories.Single(c => c.CategoryTypeId == categoryId)
            .Name.Should()
            .Be("Renombrada");
    }

    [Fact]
    public async Task Update_AfterAnonymousDetailCached_AnonymousDetailShowsNewTitle()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var announcementId = await SeedAnnouncementAsync(thumbnailId, "Título original");
        var anonymous = CreateClient();

        using var warm = await anonymous.GetAsync($"/api/announcements/{announcementId}", Ct);
        var before = await warm.ReadJsonAsync<AnnouncementResponse>(Ct);
        before!.Title.Should().Be("Título original");

        var admin = await LoginAsAdminAsync();
        using var updated = await admin.PutJsonAsync(
            $"/api/announcements/{announcementId}",
            new UpdateAnnouncementRequest("Título corregido", "Subtítulo", "{}", thumbnailId),
            Ct
        );
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        using var after = await anonymous.GetAsync($"/api/announcements/{announcementId}", Ct);
        var body = await after.ReadJsonAsync<AnnouncementResponse>(Ct);
        body!.Title.Should().Be("Título corregido");
    }

    [Fact]
    public async Task AssignHousehold_AfterAnonymousActivityCached_ActivityShowsHighDemand()
    {
        var activityId = await SeedActivityWithParticipantCapacityAsync();
        var anonymous = CreateClient();

        using var warm = await anonymous.GetAsync($"/api/activities/{activityId}", Ct);
        var before = await warm.ReadJsonAsync<ActivityResponse>(Ct);
        before!.RoleCapacities.Single().IsHighDemand.Should().BeFalse();

        var member = await LoginAsMemberAsync();
        using var assigned = await member.PostJsonAsync(
            $"/api/activities/{activityId}/assign-household",
            new AssignHouseholdRequest([
                new(TestSeedData.Users.MemberId, SeedIds.ActivityRoleTypes.Participant),
                new(TestSeedData.Users.MemberChildId, SeedIds.ActivityRoleTypes.Participant),
            ]),
            Ct
        );
        assigned.StatusCode.Should().Be(HttpStatusCode.OK);

        using var after = await anonymous.GetAsync($"/api/activities/{activityId}", Ct);
        var body = await after.ReadJsonAsync<ActivityResponse>(Ct);
        body!.RoleCapacities.Single().IsHighDemand.Should().BeTrue();
    }

    [Fact]
    public async Task GetContent_AfterFileUpdate_ServesNewBytesAndRotatedETag()
    {
        var admin = await LoginAsAdminAsync();
        using var uploaded = await admin.SendUploadAsync(
            HttpMethod.Post,
            "/api/files",
            TestSeedData.ValidPng()
        );
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var file = await uploaded.ReadJsonAsync<FileResponse>(Ct);

        var anonymous = CreateClient();
        using var first = await anonymous.GetAsync($"/api/files/{file!.Id}/content", Ct);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstEtag = first.Headers.ETag!.Tag;

        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddMinutes(1);
        var updatedBytes = TestSeedData
            .ValidPng()
            .Concat(new byte[] { 0x01, 0x02, 0x03 })
            .ToArray();
        using var updated = await admin.SendUploadAsync(
            HttpMethod.Put,
            $"/api/files/{file.Id}",
            updatedBytes
        );
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = await anonymous.GetAsync($"/api/files/{file.Id}/content", Ct);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(updatedBytes);
        second.Headers.ETag!.Tag.Should().NotBe(firstEtag);
        second.Headers.CacheControl!.NoCache.Should().BeTrue();
    }

    [Fact]
    public async Task List_Anonymous_CachesIntoTheSizeLimitedLocalCache()
    {
        await SeedEventAsync();
        var localCache = (MemoryCache)Factory.Services.GetRequiredService<IMemoryCache>();
        var localCacheOptions = Factory
            .Services.GetRequiredService<IOptions<MemoryCacheOptions>>()
            .Value;
        var before = localCache.Count;

        using var response = await CreateClient()
            .GetAsync($"/api/events?title={Guid.NewGuid():N}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        localCacheOptions.SizeLimit.Should().NotBeNull();
        localCache.Count.Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task List_Anonymous_EmitsNoStoreCacheControl()
    {
        var client = CreateClient();

        using var response = await client.GetAsync("/api/events", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Csrf_Anonymous_EmitsNoStoreCacheControl()
    {
        var client = CreateClient();

        using var response = await client.GetAsync("/api/auth/csrf", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    private static CreateEventRequest BuildCreateEvent(
        string title,
        Guid thumbnailId,
        IReadOnlyList<Guid> categoryTypeIds
    )
    {
        return new CreateEventRequest(
            title,
            "Subtítulo",
            "{}",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 10),
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            thumbnailId,
            categoryTypeIds
        );
    }

    private async Task<Guid> SeedAnnouncementAsync(Guid thumbnailId, string title)
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Announcements.Add(
                new Announcement
                {
                    Id = id,
                    Title = title,
                    Subtitle = "Subtítulo",
                    Description = "{}",
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedActivityWithParticipantCapacityAsync()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Events.Add(
                new Event
                {
                    Id = eventId,
                    Title = "Evento",
                    Subtitle = "Sub",
                    Description = "{}",
                    EventStartsAt = new DateOnly(2026, 7, 1),
                    EventEndsAt = new DateOnly(2026, 7, 31),
                    SignupStartsAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                    SignupEndsAt = new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Activities.Add(
                new Activity
                {
                    Id = activityId,
                    Title = "Actividad",
                    Description = "{}",
                    Location = "Sala",
                    ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                    ActivityStartsAt = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
                    ActivityEndsAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
                    EventId = eventId,
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                    RoleCapacities =
                    [
                        new ActivityRoleCapacity
                        {
                            ActivityId = activityId,
                            ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                            DesiredCount = 1,
                        },
                    ],
                }
            );
            return Task.CompletedTask;
        });
        return activityId;
    }

    private async Task<Guid> SeedCategoryTypeAsync(string? name = null)
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.EventCategoryTypes.Add(
                new EventCategoryType
                {
                    Id = id,
                    Name = name ?? Guid.NewGuid().ToString("N"),
                    Color = "#112233",
                }
            );
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedEventAsync(
        string title = "Evento",
        bool featured = false,
        Guid? categoryTypeId = null
    )
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = categoryTypeId ?? await SeedCategoryTypeAsync();
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            var ev = new Event
            {
                Id = id,
                Title = title,
                Subtitle = "Sub",
                Description = "{}",
                EventStartsAt = new DateOnly(2026, 8, 1),
                EventEndsAt = new DateOnly(2026, 8, 10),
                SignupStartsAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                SignupEndsAt = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
                Featured = featured,
                ThumbnailId = thumbnailId,
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            };
            ev.Categories.Add(new EventCategory { EventCategoryTypeId = categoryId });
            db.Events.Add(ev);
            return Task.CompletedTask;
        });
        return id;
    }
}
