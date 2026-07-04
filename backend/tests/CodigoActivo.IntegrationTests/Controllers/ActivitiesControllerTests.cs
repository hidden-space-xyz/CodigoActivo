using System.Net;
using System.Net.Http.Json;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

/// <summary>
/// CRUD and catalog coverage for <see cref="CodigoActivo.API.Controllers.ActivitiesController"/>:
/// anonymous reads, the admin-only write matrix, every create/update schedule/modality/thumbnail/role
/// guard, delete, and the admin-only reference-list endpoints (role/status/modality types) plus
/// role-type CRUD. Assignment flows live in <c>ActivitiesAssignmentTests</c>.
/// </summary>
public sealed class ActivitiesControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    // Event window brackets the fixed test clock (2026-07-04 12:00Z); activities fit inside the dates.
    private static readonly DateOnly EventStart = new(2026, 7, 1);
    private static readonly DateOnly EventEnd = new(2026, 7, 31);
    private static readonly DateTimeOffset SignupStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignupEnd = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivityStart = new(2026, 7, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivityEnd = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

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
                UploadedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UploadedBy = TestSeedData.Users.AdminId,
            });
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedEventAsync(Guid? thumbnailId = null, DateOnly? startsAt = null, DateOnly? endsAt = null)
    {
        var thumb = thumbnailId ?? await SeedThumbnailAsync();
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Events.Add(new Event
            {
                Id = id,
                Title = "Evento",
                Subtitle = "Sub",
                EventStartsAt = startsAt ?? EventStart,
                EventEndsAt = endsAt ?? EventEnd,
                SignupStartsAt = SignupStart,
                SignupEndsAt = SignupEnd,
                ThumbnailId = thumb,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = TestSeedData.Users.AdminId,
            });
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedActivityAsync(Guid eventId, Guid thumbnailId, string title = "Actividad")
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Activities.Add(new Activity
            {
                Id = id,
                Title = title,
                Description = "Descripcion",
                Location = "Sala",
                ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                ActivityStartsAt = ActivityStart,
                ActivityEndsAt = ActivityEnd,
                EventId = eventId,
                ThumbnailId = thumbnailId,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = TestSeedData.Users.AdminId,
            });
            return Task.CompletedTask;
        });
        return id;
    }

    private static CreateActivityRequest CreateRequest(
        Guid thumbnailId,
        Guid? modalityId = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        string title = "Nueva",
        IReadOnlyList<ActivityAllowedRoleRequest>? allowedRoles = null
    ) =>
        new(
            title,
            "Descripcion",
            "Sala",
            modalityId ?? SeedIds.ActivityModalityTypes.Presencial,
            startsAt ?? ActivityStart,
            endsAt ?? ActivityEnd,
            thumbnailId,
            allowedRoles
        );

    // ---- Reads (anonymous) -------------------------------------------------

    [Fact]
    public async Task List_is_anonymous_and_returns_paged_envelope()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        await SeedActivityAsync(eventId, thumb, "Alpha");
        var client = CreateClient();

        var response = await client.GetAsync("/api/activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ActivityResponse>>();
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.Items.Should().ContainSingle(a => a.Title == "Alpha");
    }

    [Fact]
    public async Task List_filters_by_event_id()
    {
        var thumb = await SeedThumbnailAsync();
        var eventA = await SeedEventAsync(thumb);
        var eventB = await SeedEventAsync(thumb);
        await SeedActivityAsync(eventA, thumb, "InA");
        await SeedActivityAsync(eventB, thumb, "InB");
        var client = CreateClient();

        var response = await client.GetAsync($"/api/activities?eventId={eventA}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ActivityResponse>>();
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(a => a.Title == "InA");
    }

    [Fact]
    public async Task Get_returns_activity_when_present()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var id = await SeedActivityAsync(eventId, thumb, "Beta");
        var client = CreateClient();

        var response = await client.GetAsync($"/api/activities/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var activity = await response.ReadJsonAsync<ActivityResponse>();
        activity!.Title.Should().Be("Beta");
        activity.EventId.Should().Be(eventId);
    }

    [Fact]
    public async Task Get_returns_404_ActivityNotFound_when_absent()
    {
        var client = CreateClient();

        var response = await client.GetAsync($"/api/activities/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    // ---- Create ------------------------------------------------------------

    [Fact]
    public async Task Create_as_admin_persists_and_returns_201_with_location()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(
            thumb,
            title: "Taller",
            allowedRoles: new List<ActivityAllowedRoleRequest>
            {
                new(SeedIds.ActivityRoleTypes.Leader),
            }
        );

        var response = await client.PostJsonAsync($"/api/activities/{eventId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<ActivityResponse>();
        created!.Title.Should().Be("Taller");
        created.AllowedRoleTypes.Should().ContainSingle(r => r.RoleTypeId == SeedIds.ActivityRoleTypes.Leader);

        var stored = await Factory.QueryAsync(db => db.Activities.FindAsync(created.Id).AsTask());
        stored!.EventId.Should().Be(eventId);
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
        var roles = await Factory.QueryAsync(db =>
            db.ActivityAllowedRoleTypes.Where(r => r.ActivityId == created.Id).ToListAsync());
        roles.Should().ContainSingle(r => r.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader);
    }

    [Fact]
    public async Task Create_as_member_is_forbidden()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsMemberAsync();

        var response = await client.PostJsonAsync($"/api/activities/{eventId}", CreateRequest(thumb));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            $"/api/activities/{Guid.NewGuid()}",
            CreateRequest(Guid.NewGuid())
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_missing_event_is_404_EventNotFound()
    {
        var thumb = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            $"/api/activities/{Guid.NewGuid()}",
            CreateRequest(thumb)
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task Create_with_inverted_range_is_bad_request()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        // ends <= starts
        var request = CreateRequest(thumb, startsAt: ActivityEnd, endsAt: ActivityStart);

        var response = await client.PostJsonAsync($"/api/activities/{eventId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityScheduleInvalidRange);
    }

    [Fact]
    public async Task Create_outside_event_range_is_bad_request()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        // starts after the event's last day
        var request = CreateRequest(
            thumb,
            startsAt: new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero),
            endsAt: new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero)
        );

        var response = await client.PostJsonAsync($"/api/activities/{eventId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityScheduleOutsideEventRange);
    }

    [Fact]
    public async Task Create_with_missing_thumbnail_is_bad_request()
    {
        var eventId = await SeedEventAsync();
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(Guid.NewGuid());

        var response = await client.PostJsonAsync($"/api/activities/{eventId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityThumbnailNotFound);
    }

    [Fact]
    public async Task Create_with_unknown_modality_is_bad_request()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(thumb, modalityId: Guid.NewGuid());

        var response = await client.PostJsonAsync($"/api/activities/{eventId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityModalityTypeNotFound);
    }

    [Fact]
    public async Task Create_with_unknown_allowed_role_is_bad_request()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(
            thumb,
            allowedRoles: new List<ActivityAllowedRoleRequest> { new(Guid.NewGuid()) }
        );

        var response = await client.PostJsonAsync($"/api/activities/{eventId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
    }

    [Fact]
    public async Task Create_with_blank_title_is_validation_error()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(thumb, title: "   ");

        var response = await client.PostJsonAsync($"/api/activities/{eventId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Create_without_csrf_token_is_rejected()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/activities/{eventId}")
        {
            Content = JsonContent.Create(CreateRequest(thumb), options: TestJson.Options),
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }

    // ---- Update ------------------------------------------------------------

    [Fact]
    public async Task Update_as_admin_changes_activity_and_replaces_roles()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var id = await SeedActivityAsync(eventId, thumb, "Antes");
        var client = await LoginAsAdminAsync();
        var request = new UpdateActivityRequest(
            "Despues",
            "Descripcion",
            "Otra Sala",
            SeedIds.ActivityModalityTypes.Online,
            ActivityStart,
            ActivityEnd,
            thumb,
            new List<ActivityAllowedRoleRequest> { new(SeedIds.ActivityRoleTypes.Helper) }
        );

        var response = await client.PutJsonAsync($"/api/activities/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db => db.Activities.FindAsync(id).AsTask());
        stored!.Title.Should().Be("Despues");
        stored.ActivityModalityTypeId.Should().Be(SeedIds.ActivityModalityTypes.Online);
        stored.UpdatedBy.Should().Be(TestSeedData.Users.AdminId);
        var roles = await Factory.QueryAsync(db =>
            db.ActivityAllowedRoleTypes.Where(r => r.ActivityId == id).ToListAsync());
        roles.Should().ContainSingle(r => r.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Helper);
    }

    [Fact]
    public async Task Update_missing_activity_is_404_ActivityNotFound()
    {
        var thumb = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdateActivityRequest(
            "X", "Descripcion", "Sala", SeedIds.ActivityModalityTypes.Presencial,
            ActivityStart, ActivityEnd, thumb, null
        );

        var response = await client.PutJsonAsync($"/api/activities/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task Update_with_orphan_event_is_404_EventNotFound()
    {
        var thumb = await SeedThumbnailAsync();
        // Activity points at an event id that was never seeded (in-memory ignores the FK).
        var id = await SeedActivityAsync(Guid.NewGuid(), thumb);
        var client = await LoginAsAdminAsync();
        var request = new UpdateActivityRequest(
            "X", "Descripcion", "Sala", SeedIds.ActivityModalityTypes.Presencial,
            ActivityStart, ActivityEnd, thumb, null
        );

        var response = await client.PutJsonAsync($"/api/activities/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task Update_outside_event_range_is_bad_request()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var id = await SeedActivityAsync(eventId, thumb);
        var client = await LoginAsAdminAsync();
        var request = new UpdateActivityRequest(
            "X", "Descripcion", "Sala", SeedIds.ActivityModalityTypes.Presencial,
            new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
            thumb, null
        );

        var response = await client.PutJsonAsync($"/api/activities/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityScheduleOutsideEventRange);
    }

    // ---- Delete ------------------------------------------------------------

    [Fact]
    public async Task Delete_as_admin_removes_activity()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var id = await SeedActivityAsync(eventId, thumb);
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/activities/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.Activities.FindAsync(id).AsTask());
        stored.Should().BeNull();
    }

    [Fact]
    public async Task Delete_missing_activity_is_404_ActivityNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/activities/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    // ---- Catalog reads (admin only) ---------------------------------------

    [Fact]
    public async Task RoleTypes_lists_seeded_roles_for_admin()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/activities/roleType");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var roles = await response.ReadJsonAsync<IReadOnlyList<ActivityRoleTypeResponse>>();
        roles!.Should().Contain(r => r.Id == SeedIds.ActivityRoleTypes.Leader);
        roles.Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public async Task RoleTypes_as_member_is_forbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/activities/roleType");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignmentStatusTypes_lists_seeded_statuses_for_admin()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/activities/assignment-status-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var statuses = await response.ReadJsonAsync<IReadOnlyList<AssignmentStatusTypeResponse>>();
        statuses!.Should().Contain(s => s.Id == SeedIds.AssignmentStatusTypes.Requested);
    }

    [Fact]
    public async Task ModalityTypes_lists_seeded_modalities_for_admin()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/activities/modality-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var modalities = await response.ReadJsonAsync<IReadOnlyList<ActivityModalityTypeResponse>>();
        modalities!.Should().Contain(m => m.Id == SeedIds.ActivityModalityTypes.Presencial);
    }

    // ---- Role-type CRUD (admin only) --------------------------------------

    [Fact]
    public async Task CreateRoleType_as_admin_persists()
    {
        var client = await LoginAsAdminAsync();
        var request = new CreateActivityRoleTypeRequest("Coordinador", "Coordina la actividad");

        var response = await client.PostJsonAsync("/api/activities/roleType", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<ActivityRoleTypeResponse>();
        created!.Name.Should().Be("Coordinador");
        var stored = await Factory.QueryAsync(db =>
            db.ActivityRoleTypes.FirstOrDefaultAsync(r => r.Name == "Coordinador"));
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateRoleType_with_duplicate_name_is_conflict()
    {
        var client = await LoginAsAdminAsync();
        var request = new CreateActivityRoleTypeRequest("Líder", null);

        var response = await client.PostJsonAsync("/api/activities/roleType", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityRoleTypeNameAlreadyExists);
    }

    [Fact]
    public async Task UpdateRoleType_as_admin_changes_name()
    {
        var client = await LoginAsAdminAsync();
        var request = new UpdateActivityRoleTypeRequest("Ayudante Senior", "Actualizado");

        var response = await client.PutJsonAsync(
            $"/api/activities/roleType/{SeedIds.ActivityRoleTypes.Helper}",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.ActivityRoleTypes.FindAsync(SeedIds.ActivityRoleTypes.Helper).AsTask());
        stored!.Name.Should().Be("Ayudante Senior");
    }

    [Fact]
    public async Task UpdateRoleType_missing_is_404()
    {
        var client = await LoginAsAdminAsync();
        var request = new UpdateActivityRoleTypeRequest("X", null);

        var response = await client.PutJsonAsync(
            $"/api/activities/roleType/{Guid.NewGuid()}",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
    }

    [Fact]
    public async Task DeleteRoleType_as_admin_removes()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/activities/roleType/{SeedIds.ActivityRoleTypes.Participant}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.ActivityRoleTypes.FindAsync(SeedIds.ActivityRoleTypes.Participant).AsTask());
        stored.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRoleType_missing_is_404()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/activities/roleType/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
    }
}
