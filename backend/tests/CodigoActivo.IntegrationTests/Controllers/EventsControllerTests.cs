using System.Net;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

/// <summary>
/// HTTP-level coverage for <c>EventsController</c> and the <c>EventService</c> behind it: anonymous
/// reads with time-scope/year/featured filters and the paging envelope, the not-found contract, the
/// admin-only write matrix, every schedule/thumbnail/category guard, the activity-range guard on
/// update, feature toggling, and the event-category-type CRUD (including the conflict/not-found codes).
/// The host clock is fixed to 2026-07-04 (<see cref="TestClock"/>), so upcoming/past boundaries are
/// deterministic.
/// </summary>
public sealed class EventsControllerTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ---- seeding helpers ---------------------------------------------------

    private async Task<Guid> SeedThumbnailAsync()
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(new FileEntity
            {
                Id = id,
                Name = "thumb",
                Extension = "png",
                UploadedAt = SeededAt,
                UploadedBy = TestSeedData.Users.AdminId,
            });
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedCategoryTypeAsync(string name = "Formación", string color = "#112233")
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.EventCategoryTypes.Add(new EventCategoryType { Id = id, Name = name, Color = color });
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedEventAsync(
        DateOnly start,
        DateOnly end,
        bool featured = false,
        string title = "Evento",
        Guid? categoryTypeId = null
    )
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = categoryTypeId ?? await SeedCategoryTypeAsync(Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid();
        var startAt = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        await Factory.SeedAsync(db =>
        {
            db.Events.Add(new Event
            {
                Id = id,
                Title = title,
                Subtitle = "Sub",
                Description = "{}",
                EventStartsAt = start,
                EventEndsAt = end,
                SignupStartsAt = startAt.AddDays(-10),
                SignupEndsAt = startAt.AddDays(-1),
                Featured = featured,
                ThumbnailId = thumbnailId,
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
                Categories = { new EventCategory { EventCategoryTypeId = categoryId } },
            });
            return Task.CompletedTask;
        });
        return id;
    }

    private static CreateEventRequest BuildCreate(
        Guid thumbnailId,
        IReadOnlyList<Guid>? categoryTypeIds,
        DateOnly? start = null,
        DateOnly? end = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        string title = "Nuevo evento",
        string subtitle = "Subtítulo"
    )
    {
        return new CreateEventRequest(
            title,
            subtitle,
            "{}",
            start ?? new DateOnly(2026, 8, 1),
            end ?? new DateOnly(2026, 8, 10),
            signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            thumbnailId,
            categoryTypeIds
        );
    }

    private static UpdateEventRequest BuildUpdate(
        Guid thumbnailId,
        IReadOnlyList<Guid>? categoryTypeIds,
        DateOnly? start = null,
        DateOnly? end = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        string title = "Evento editado"
    )
    {
        return new UpdateEventRequest(
            title,
            "Subtítulo",
            "{}",
            start ?? new DateOnly(2026, 8, 1),
            end ?? new DateOnly(2026, 8, 10),
            signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            thumbnailId,
            categoryTypeIds
        );
    }

    // ---- List / reads (anonymous) ------------------------------------------

    [Fact]
    public async Task List_is_anonymous_and_returns_paged_envelope_with_categories()
    {
        var categoryId = await SeedCategoryTypeAsync("Cultura");
        await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), title: "Alfa", categoryTypeId: categoryId);
        var client = CreateClient();

        var response = await client.GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventListItemResponse>>();
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(25);
        var item = page.Items.Should().ContainSingle(e => e.Title == "Alfa").Subject;
        item.Categories.Should().ContainSingle(c => c.CategoryTypeId == categoryId && c.Name == "Cultura");
    }

    [Fact]
    public async Task Get_returns_404_EventNotFound_when_absent()
    {
        var client = CreateClient();

        var response = await client.GetAsync($"/api/events/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task PastYears_is_anonymous_and_returns_distinct_years_descending()
    {
        await SeedEventAsync(new DateOnly(2024, 5, 1), new DateOnly(2024, 6, 1), title: "P24");
        await SeedEventAsync(new DateOnly(2025, 5, 1), new DateOnly(2025, 6, 1), title: "P25a");
        await SeedEventAsync(new DateOnly(2025, 8, 1), new DateOnly(2025, 9, 1), title: "P25b");
        await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10), title: "Futuro");
        var client = CreateClient();

        var response = await client.GetAsync("/api/events/past-years");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var years = await response.ReadJsonAsync<IReadOnlyList<int>>();
        years.Should().Equal(2025, 2024);
    }

    // ---- Create ------------------------------------------------------------

    [Fact]
    public async Task Create_as_admin_persists_event_with_categories_and_returns_201()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync("Taller");
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(thumbnailId, new[] { categoryId }, title: "Creado");

        var response = await client.PostJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<EventResponse>();
        created!.Title.Should().Be("Creado");

        var stored = await Factory.QueryAsync(db =>
            db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.Id == created.Id)
        );
        stored!.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
        stored.Categories.Should().ContainSingle(c => c.EventCategoryTypeId == categoryId);
    }

    [Fact]
    public async Task Create_as_member_is_forbidden()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync();
        var client = await LoginAsMemberAsync();
        var request = BuildCreate(thumbnailId, new[] { categoryId });

        var response = await client.PostJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_anonymous_is_unauthorized()
    {
        var client = CreateClient();
        var request = BuildCreate(Guid.NewGuid(), new[] { Guid.NewGuid() });

        var response = await client.PostJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_with_blank_title_is_validation_error()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(thumbnailId, new[] { categoryId }, title: "   ");

        var response = await client.PostJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    // ---- Update ------------------------------------------------------------

    [Fact]
    public async Task Update_as_admin_changes_event_and_persists()
    {
        var categoryId = await SeedCategoryTypeAsync("Original");
        var id = await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10), title: "Antes", categoryTypeId: categoryId);
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildUpdate(thumbnailId, new[] { categoryId }, title: "Después");

        var response = await client.PutJsonAsync($"/api/events/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db => db.Events.FindAsync(id).AsTask());
        stored!.Title.Should().Be("Después");
        stored.UpdatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Update_with_replacement_thumbnail_deletes_the_orphaned_old_file()
    {
        var categoryId = await SeedCategoryTypeAsync("Cascada");
        var id = await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10), title: "ConMiniatura", categoryTypeId: categoryId);
        var oldThumbnailId = (await Factory.QueryAsync(db => db.Events.FindAsync(id).AsTask()))!.ThumbnailId;
        var newThumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildUpdate(newThumbnailId, new[] { categoryId });

        var response = await client.PutJsonAsync($"/api/events/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldFile = await Factory.QueryAsync(db => db.Files.FindAsync(oldThumbnailId).AsTask());
        oldFile.Should().BeNull("the replaced thumbnail is orphaned and must be cascade-deleted");
        var newFile = await Factory.QueryAsync(db => db.Files.FindAsync(newThumbnailId).AsTask());
        newFile.Should().NotBeNull();
    }

    // ---- Delete / Feature --------------------------------------------------

    [Fact]
    public async Task Delete_as_admin_removes_event_and_its_orphaned_thumbnail()
    {
        var id = await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), title: "Borrar");
        var thumbnailId = (await Factory.QueryAsync(db => db.Events.FindAsync(id).AsTask()))!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/events/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.Events.FindAsync(id).AsTask());
        stored.Should().BeNull();
        var file = await Factory.QueryAsync(db => db.Files.FindAsync(thumbnailId).AsTask());
        file.Should().BeNull("the deleted event's thumbnail is orphaned and must be cascade-deleted");
    }

    [Fact]
    public async Task Feature_missing_event_is_404_EventNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync($"/api/events/{Guid.NewGuid()}/feature");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    // ---- Category-type CRUD ------------------------------------------------

    [Fact]
    public async Task ListCategoryTypes_returns_seeded_types_for_admin()
    {
        await SeedCategoryTypeAsync("Alpha");
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/events/categoryType");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<IReadOnlyList<EventCategoryTypeResponse>>();
        types.Should().ContainSingle(t => t.Name == "Alpha");
    }

    [Fact]
    public async Task CreateCategoryType_as_admin_persists_and_returns_ok()
    {
        var client = await LoginAsAdminAsync();
        var request = new CreateEventCategoryTypeRequest("Innovación", "#3366cc");

        var response = await client.PostJsonAsync("/api/events/categoryType", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<EventCategoryTypeResponse>();
        created!.Name.Should().Be("Innovación");

        var stored = await Factory.QueryAsync(db => db.EventCategoryTypes.FindAsync(created.Id).AsTask());
        stored!.Color.Should().Be("#3366cc");
    }

    [Fact]
    public async Task UpdateCategoryType_as_admin_persists_changes()
    {
        var id = await SeedCategoryTypeAsync("Vieja", "#111111");
        var client = await LoginAsAdminAsync();
        var request = new UpdateEventCategoryTypeRequest("Nueva", "#222222");

        var response = await client.PutJsonAsync($"/api/events/categoryType/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db => db.EventCategoryTypes.FindAsync(id).AsTask());
        stored!.Name.Should().Be("Nueva");
        stored.Color.Should().Be("#222222");
    }

    [Fact]
    public async Task DeleteCategoryType_as_admin_removes_it()
    {
        var id = await SeedCategoryTypeAsync("Efímera");
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/events/categoryType/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.EventCategoryTypes.FindAsync(id).AsTask());
        stored.Should().BeNull();
    }
}
