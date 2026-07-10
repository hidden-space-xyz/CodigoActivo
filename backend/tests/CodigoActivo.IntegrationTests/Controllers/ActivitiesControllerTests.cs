using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class ActivitiesControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateOnly EventStart = new(2026, 7, 1);
    private static readonly DateOnly EventEnd = new(2026, 7, 31);
    private static readonly DateTimeOffset SignupStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignupEnd = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivityStart = new(
        2026,
        7,
        10,
        10,
        0,
        0,
        TimeSpan.Zero
    );
    private static readonly DateTimeOffset ActivityEnd = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    private async Task<Guid> SeedThumbnailAsync()
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = id,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedEventAsync(
        Guid? thumbnailId = null,
        DateOnly? startsAt = null,
        DateOnly? endsAt = null
    )
    {
        var thumb = thumbnailId ?? await SeedThumbnailAsync();
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Events.Add(
                new Event
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
                }
            );
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedActivityAsync(
        Guid eventId,
        Guid thumbnailId,
        string title = "Actividad"
    )
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Activities.Add(
                new Activity
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
                }
            );
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

    [Fact]
    public async Task List_Anonymous_ReturnsPagedEnvelope()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        await SeedActivityAsync(eventId, thumb, "Alpha");
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/activities",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ActivityResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.Items.Should().ContainSingle(a => a.Title == "Alpha");
    }

    [Fact]
    public async Task Get_ActivityAbsent_Returns404ActivityNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/activities/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task Create_AsAdmin_PersistsAndReturns201WithLocation()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(
            thumb,
            title: "Taller",
            allowedRoles: [new(SeedIds.ActivityRoleTypes.Leader)]
        );

        var response = await client.PostJsonAsync(
            $"/api/activities/{eventId}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<ActivityResponse>(
            TestContext.Current.CancellationToken
        );
        created!.Title.Should().Be("Taller");
        created
            .AllowedRoleTypes.Should()
            .ContainSingle(r => r.RoleTypeId == SeedIds.ActivityRoleTypes.Leader);

        var stored = await Factory.QueryAsync(db =>
            db.Activities.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.EventId.Should().Be(eventId);
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
        var roles = await Factory.QueryAsync(db =>
            db.ActivityAllowedRoleTypes.Where(r => r.ActivityId == created.Id)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        roles.Should().ContainSingle(r => r.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader);
    }

    [Fact]
    public async Task Create_AsMember_ReturnsForbidden()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsMemberAsync();

        var response = await client.PostJsonAsync(
            $"/api/activities/{eventId}",
            CreateRequest(thumb),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.PostJsonAsync(
            $"/api/activities/{Guid.NewGuid()}",
            CreateRequest(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_BlankTitle_ReturnsValidationError()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(thumb, title: "   ");

        var response = await client.PostJsonAsync(
            $"/api/activities/{eventId}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Create_MissingCsrfToken_IsRejected()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/activities/{eventId}")
        {
            Content = JsonContent.Create(CreateRequest(thumb), options: TestJson.Options),
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task Update_AsAdmin_ChangesActivityAndReplacesRoles()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var id = await SeedActivityAsync(eventId, thumb, "Antes");
        await Factory.SeedAsync(db =>
        {
            db.ActivityAllowedRoleTypes.Add(
                new ActivityAllowedRoleType
                {
                    ActivityId = id,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Leader,
                }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();
        var request = new UpdateActivityRequest(
            "Despues",
            "Descripcion",
            "Otra Sala",
            SeedIds.ActivityModalityTypes.Online,
            ActivityStart,
            ActivityEnd,
            thumb,
            [new(SeedIds.ActivityRoleTypes.Helper)]
        );

        var response = await client.PutJsonAsync(
            $"/api/activities/{id}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Activities.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Title.Should().Be("Despues");
        stored.ActivityModalityTypeId.Should().Be(SeedIds.ActivityModalityTypes.Online);
        stored.UpdatedBy.Should().Be(TestSeedData.Users.AdminId);
        var roles = await Factory.QueryAsync(db =>
            db.ActivityAllowedRoleTypes.Where(r => r.ActivityId == id)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        roles
            .Should()
            .ContainSingle()
            .Which.ActivityRoleTypeId.Should()
            .Be(SeedIds.ActivityRoleTypes.Helper);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesActivityAndOrphanedThumbnail()
    {
        var eventThumb = await SeedThumbnailAsync();
        var activityThumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(eventThumb);
        var id = await SeedActivityAsync(eventId, activityThumb);
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/activities/{id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.Activities.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored.Should().BeNull();
        var file = await Factory.QueryAsync(db =>
            db.Files.FindAsync([activityThumb], TestContext.Current.CancellationToken).AsTask()
        );
        file.Should()
            .BeNull("the deleted activity's thumbnail is orphaned and must be cascade-deleted");
    }

    [Fact]
    public async Task Delete_ThumbnailSharedWithEvent_KeepsThumbnail()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var id = await SeedActivityAsync(eventId, thumb);
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/activities/{id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var file = await Factory.QueryAsync(db =>
            db.Files.FindAsync([thumb], TestContext.Current.CancellationToken).AsTask()
        );
        file.Should()
            .NotBeNull("a thumbnail still referenced by another entity must survive the cascade");
    }

    [Fact]
    public async Task RoleTypes_AsAdmin_ListsSeededRoles()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/activities/roleType",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var roles = await response.ReadJsonAsync<IReadOnlyList<ActivityRoleTypeResponse>>(
            TestContext.Current.CancellationToken
        );
        roles!.Should().Contain(r => r.Id == SeedIds.ActivityRoleTypes.Leader);
        roles.Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public async Task AssignmentStatusTypes_AsAdmin_ListsSeededStatuses()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/activities/assignment-status-types",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var statuses = await response.ReadJsonAsync<IReadOnlyList<AssignmentStatusTypeResponse>>(
            TestContext.Current.CancellationToken
        );
        statuses!.Should().Contain(s => s.Id == SeedIds.AssignmentStatusTypes.Requested);
    }

    [Fact]
    public async Task ModalityTypes_AsAdmin_ListsSeededModalities()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/activities/modality-types",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var modalities = await response.ReadJsonAsync<IReadOnlyList<ActivityModalityTypeResponse>>(
            TestContext.Current.CancellationToken
        );
        modalities!.Should().Contain(m => m.Id == SeedIds.ActivityModalityTypes.Presencial);
    }

    [Fact]
    public async Task CreateRoleType_AsAdmin_Persists()
    {
        var client = await LoginAsAdminAsync();
        var request = new CreateActivityRoleTypeRequest("Coordinador", "Coordina la actividad");

        var response = await client.PostJsonAsync(
            "/api/activities/roleType",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<ActivityRoleTypeResponse>(
            TestContext.Current.CancellationToken
        );
        created!.Name.Should().Be("Coordinador");
        var stored = await Factory.QueryAsync(db =>
            db.ActivityRoleTypes.FirstOrDefaultAsync(
                r => r.Name == "Coordinador",
                TestContext.Current.CancellationToken
            )
        );
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateRoleType_AsAdmin_ChangesName()
    {
        var client = await LoginAsAdminAsync();
        var request = new UpdateActivityRoleTypeRequest("Ayudante Senior", "Actualizado");

        var response = await client.PutJsonAsync(
            $"/api/activities/roleType/{SeedIds.ActivityRoleTypes.Helper}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.ActivityRoleTypes.FindAsync(
                    [SeedIds.ActivityRoleTypes.Helper],
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );
        stored!.Name.Should().Be("Ayudante Senior");
    }

    [Fact]
    public async Task DeleteRoleType_AsAdmin_Removes()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/activities/roleType/{SeedIds.ActivityRoleTypes.Participant}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.ActivityRoleTypes.FindAsync(
                    [SeedIds.ActivityRoleTypes.Participant],
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );
        stored.Should().BeNull();
    }
}
