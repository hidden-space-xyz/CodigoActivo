using System.Net;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using FluentAssertions;
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

    private async Task SeedActivityAsync(Guid eventId, DateTimeOffset start, DateTimeOffset end)
    {
        var thumbnailId = await SeedThumbnailAsync();
        await Factory.SeedAsync(db =>
        {
            db.Activities.Add(new Activity
            {
                Id = Guid.NewGuid(),
                Title = "Taller",
                Description = "{}",
                Location = "Aula",
                ActivityStartsAt = start,
                ActivityEndsAt = end,
                EventId = eventId,
                ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                ThumbnailId = thumbnailId,
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            });
            return Task.CompletedTask;
        });
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
        var page = await response.ReadJsonAsync<PagedResult<EventResponse>>();
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(25);
        var item = page.Items.Should().ContainSingle(e => e.Title == "Alfa").Subject;
        item.Categories.Should().ContainSingle(c => c.CategoryTypeId == categoryId && c.Name == "Cultura");
    }

    [Fact]
    public async Task List_upcoming_scope_returns_only_events_ending_on_or_after_today()
    {
        await SeedEventAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), title: "Pasado");
        await SeedEventAsync(new DateOnly(2026, 7, 4), new DateOnly(2026, 7, 10), title: "Futuro");
        var client = CreateClient();

        var response = await client.GetAsync("/api/events?scope=Upcoming");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventResponse>>();
        page!.Items.Should().ContainSingle(e => e.Title == "Futuro");
    }

    [Fact]
    public async Task List_past_scope_returns_only_events_ending_before_today()
    {
        await SeedEventAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), title: "Pasado");
        await SeedEventAsync(new DateOnly(2026, 7, 4), new DateOnly(2026, 7, 10), title: "Futuro");
        var client = CreateClient();

        var response = await client.GetAsync("/api/events?scope=Past");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventResponse>>();
        page!.Items.Should().ContainSingle(e => e.Title == "Pasado");
    }

    [Fact]
    public async Task List_year_filter_matches_event_start_year()
    {
        await SeedEventAsync(new DateOnly(2025, 5, 1), new DateOnly(2025, 5, 5), title: "DeDosMilVeinticinco");
        await SeedEventAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5), title: "DeDosMilVeintiseis");
        var client = CreateClient();

        var response = await client.GetAsync("/api/events?year=2025");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventResponse>>();
        page!.Items.Should().ContainSingle(e => e.Title == "DeDosMilVeinticinco");
    }

    [Fact]
    public async Task List_featured_filter_returns_only_featured_events()
    {
        await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), featured: true, title: "Destacado");
        await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), featured: false, title: "Normal");
        var client = CreateClient();

        var response = await client.GetAsync("/api/events?featured=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventResponse>>();
        page!.Items.Should().ContainSingle(e => e.Title == "Destacado" && e.Featured);
    }

    [Fact]
    public async Task List_honours_page_size_and_reports_total()
    {
        await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), title: "Uno");
        await SeedEventAsync(new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 6), title: "Dos");
        await SeedEventAsync(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 7), title: "Tres");
        var client = CreateClient();

        var response = await client.GetAsync("/api/events?pageSize=2&page=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventResponse>>();
        page!.Total.Should().Be(3);
        page.PageSize.Should().Be(2);
        page.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_returns_event_when_present()
    {
        var id = await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), title: "Detalle");
        var client = CreateClient();

        var response = await client.GetAsync($"/api/events/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<EventResponse>();
        body!.Title.Should().Be("Detalle");
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

    [Theory]
    // eventEnd < eventStart
    [InlineData("2026-08-10", "2026-08-01", "2026-07-01T00:00:00Z", "2026-07-02T00:00:00Z")]
    // signupEnd <= signupStart (equal)
    [InlineData("2026-08-01", "2026-08-10", "2026-07-02T00:00:00Z", "2026-07-02T00:00:00Z")]
    // signup start date falls after the event end
    [InlineData("2026-08-01", "2026-08-10", "2026-08-20T00:00:00Z", "2026-08-21T00:00:00Z")]
    public async Task Create_with_invalid_schedule_is_bad_request(
        string start,
        string end,
        string signupStart,
        string signupEnd
    )
    {
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(
            Guid.NewGuid(),
            new[] { Guid.NewGuid() },
            DateOnly.Parse(start),
            DateOnly.Parse(end),
            DateTimeOffset.Parse(signupStart),
            DateTimeOffset.Parse(signupEnd)
        );

        var response = await client.PostJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventScheduleInvalidRange);
    }

    [Fact]
    public async Task Create_with_missing_thumbnail_is_bad_request()
    {
        var categoryId = await SeedCategoryTypeAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(Guid.NewGuid(), new[] { categoryId });

        var response = await client.PostJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventThumbnailNotFound);
    }

    [Fact]
    public async Task Create_without_categories_is_bad_request()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(thumbnailId, Array.Empty<Guid>());

        var response = await client.PostJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventCategoriesRequired);
    }

    [Fact]
    public async Task Create_with_unknown_category_is_bad_request()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(thumbnailId, new[] { Guid.NewGuid() });

        var response = await client.PostJsonAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventCategoryTypeNotFound);
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
    public async Task Update_missing_event_is_404_EventNotFound()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildUpdate(thumbnailId, new[] { categoryId });

        var response = await client.PutJsonAsync($"/api/events/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task Update_rejected_when_an_activity_falls_outside_the_new_range()
    {
        var categoryId = await SeedCategoryTypeAsync("Rango");
        var id = await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10), title: "ConActividad", categoryTypeId: categoryId);
        await SeedActivityAsync(
            id,
            new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 5, 11, 0, 0, TimeSpan.Zero)
        );
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildUpdate(
            thumbnailId,
            new[] { categoryId },
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 5),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero)
        );

        var response = await client.PutJsonAsync($"/api/events/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventActivitiesOutsideNewRange);

        var stored = await Factory.QueryAsync(db => db.Events.FindAsync(id).AsTask());
        stored!.EventStartsAt.Should().Be(new DateOnly(2026, 8, 1));
    }

    // ---- Delete / Feature --------------------------------------------------

    [Fact]
    public async Task Delete_as_admin_removes_event()
    {
        var id = await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), title: "Borrar");
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/events/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.Events.FindAsync(id).AsTask());
        stored.Should().BeNull();
    }

    [Fact]
    public async Task Delete_missing_event_is_404_EventNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/events/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact(Skip = "SetFeatured -> repo.SetFeaturedAsync uses EF ExecuteUpdateAsync, unsupported by the in-memory provider (needs a relational DB / Docker). Service logic covered by EventService unit tests.")]
    public async Task Feature_as_admin_marks_event_featured()
    {
        var id = await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), featured: false, title: "AFeaturear");
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync($"/api/events/{id}/feature");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<EventResponse>();
        body!.Featured.Should().BeTrue();
        var stored = await Factory.QueryAsync(db => db.Events.FindAsync(id).AsTask());
        stored!.Featured.Should().BeTrue();
    }

    [Fact(Skip = "SetFeatured -> repo.SetFeaturedAsync uses EF ExecuteUpdateAsync, unsupported by the in-memory provider (needs a relational DB / Docker). Service logic covered by EventService unit tests.")]
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
    public async Task ListCategoryTypes_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/events/categoryType");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
    public async Task CreateCategoryType_with_duplicate_name_is_conflict()
    {
        await SeedCategoryTypeAsync("Duplicada");
        var client = await LoginAsAdminAsync();
        var request = new CreateEventCategoryTypeRequest("Duplicada", "#3366cc");

        var response = await client.PostJsonAsync("/api/events/categoryType", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventCategoryTypeNameAlreadyExists);
    }

    [Fact]
    public async Task CreateCategoryType_with_invalid_color_is_validation_error()
    {
        var client = await LoginAsAdminAsync();
        var request = new CreateEventCategoryTypeRequest("MalColor", "notacolor");

        var response = await client.PostJsonAsync("/api/events/categoryType", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task CreateCategoryType_as_member_is_forbidden()
    {
        var client = await LoginAsMemberAsync();
        var request = new CreateEventCategoryTypeRequest("NoPuede", "#3366cc");

        var response = await client.PostJsonAsync("/api/events/categoryType", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
    public async Task UpdateCategoryType_missing_is_404()
    {
        var client = await LoginAsAdminAsync();
        var request = new UpdateEventCategoryTypeRequest("Fantasma", "#222222");

        var response = await client.PutJsonAsync($"/api/events/categoryType/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventCategoryTypeNotFound);
    }

    [Fact]
    public async Task UpdateCategoryType_to_existing_name_is_conflict()
    {
        await SeedCategoryTypeAsync("Ocupada");
        var id = await SeedCategoryTypeAsync("Libre");
        var client = await LoginAsAdminAsync();
        var request = new UpdateEventCategoryTypeRequest("Ocupada", "#222222");

        var response = await client.PutJsonAsync($"/api/events/categoryType/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventCategoryTypeNameAlreadyExists);
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

    [Fact]
    public async Task DeleteCategoryType_missing_is_404()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/events/categoryType/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventCategoryTypeNotFound);
    }
}
