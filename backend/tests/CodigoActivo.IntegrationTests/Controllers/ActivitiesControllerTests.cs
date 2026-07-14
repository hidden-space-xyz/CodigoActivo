using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
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
        string title = "Actividad",
        Guid? modalityId = null,
        string location = "Sala",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null
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
                    Location = location,
                    ActivityModalityTypeId = modalityId ?? SeedIds.ActivityModalityTypes.Presencial,
                    ActivityStartsAt = startsAt ?? ActivityStart,
                    ActivityEndsAt = endsAt ?? ActivityEnd,
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
        IReadOnlyList<ActivityRoleCapacityRequest>? roleCapacities = null
    ) =>
        new(
            title,
            "Descripcion",
            "Sala",
            modalityId ?? SeedIds.ActivityModalityTypes.Presencial,
            startsAt ?? ActivityStart,
            endsAt ?? ActivityEnd,
            thumbnailId,
            roleCapacities
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
    public async Task List_ModalityTypeIdFilter_ReturnsOnlyMatchingModality()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        await SeedActivityAsync(
            eventId,
            thumb,
            "En sala",
            SeedIds.ActivityModalityTypes.Presencial
        );
        await SeedActivityAsync(eventId, thumb, "En remoto", SeedIds.ActivityModalityTypes.Online);
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/activities?modalityTypeId={SeedIds.ActivityModalityTypes.Online}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ActivityResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        var item = page.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("En remoto");
        item.ModalityId.Should().Be(SeedIds.ActivityModalityTypes.Online);
    }

    [Fact]
    public async Task List_LocationFilter_MatchesAccentAndCaseInsensitively()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        await SeedActivityAsync(eventId, thumb, "Ponencia", location: "Salón de actos");
        await SeedActivityAsync(eventId, thumb, "Lectura", location: "Biblioteca");
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/activities?location=SALON",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ActivityResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(a => a.Title == "Ponencia");
    }

    [Fact]
    public async Task List_FilterByActivityDateRange_MatchesActivitiesOverlappingRange()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        await SeedActivityAsync(
            eventId,
            thumb,
            "Temprana",
            startsAt: new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            endsAt: new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)
        );
        await SeedActivityAsync(
            eventId,
            thumb,
            "Tardia",
            startsAt: new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
            endsAt: new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero)
        );
        await SeedActivityAsync(
            eventId,
            thumb,
            "Nocturna",
            startsAt: new DateTimeOffset(2026, 7, 12, 23, 0, 0, TimeSpan.Zero),
            endsAt: new DateTimeOffset(2026, 7, 13, 1, 0, 0, TimeSpan.Zero)
        );
        var client = CreateClient();

        var fromResponse = await client.GetAsync(
            "/api/activities?activityDateFrom=2026-07-13",
            TestContext.Current.CancellationToken
        );
        var toResponse = await client.GetAsync(
            "/api/activities?activityDateTo=2026-07-12",
            TestContext.Current.CancellationToken
        );

        fromResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fromPage = await fromResponse.ReadJsonAsync<PagedResult<ActivityResponse>>(
            TestContext.Current.CancellationToken
        );
        fromPage!.Total.Should().Be(2);
        fromPage.Items.Select(a => a.Title).Should().BeEquivalentTo("Tardia", "Nocturna");

        toResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var toPage = await toResponse.ReadJsonAsync<PagedResult<ActivityResponse>>(
            TestContext.Current.CancellationToken
        );
        toPage!.Total.Should().Be(2);
        toPage.Items.Select(a => a.Title).Should().BeEquivalentTo("Temprana", "Nocturna");
    }

    [Fact]
    public async Task List_SortByLocation_OrdersByLocation()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        await SeedActivityAsync(eventId, thumb, "Dos", location: "Sala 2");
        await SeedActivityAsync(eventId, thumb, "Uno", location: "Auditorio");
        await SeedActivityAsync(eventId, thumb, "Tres", location: "Biblioteca");
        var client = CreateClient();

        var ascending = await client.GetAsync(
            "/api/activities?sort=location",
            TestContext.Current.CancellationToken
        );
        var descending = await client.GetAsync(
            "/api/activities?sort=-location",
            TestContext.Current.CancellationToken
        );

        ascending.StatusCode.Should().Be(HttpStatusCode.OK);
        var ascendingPage = await ascending.ReadJsonAsync<PagedResult<ActivityResponse>>(
            TestContext.Current.CancellationToken
        );
        ascendingPage!.Items.Select(a => a.Title).Should().Equal("Uno", "Tres", "Dos");

        descending.StatusCode.Should().Be(HttpStatusCode.OK);
        var descendingPage = await descending.ReadJsonAsync<PagedResult<ActivityResponse>>(
            TestContext.Current.CancellationToken
        );
        descendingPage!.Items.Select(a => a.Title).Should().Equal("Dos", "Tres", "Uno");
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
        var request = CreateRequest(thumb, title: "Taller");

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

        var stored = await Factory.QueryAsync(db =>
            db.Activities.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.EventId.Should().Be(eventId);
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Create_WithRoleCapacities_PersistsAndReturnsThem()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(
            thumb,
            roleCapacities:
            [
                new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 10),
                new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Leader, 1),
            ]
        );

        var response = await client.PostJsonAsync(
            $"/api/activities/{eventId}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<ActivityResponse>(
            TestContext.Current.CancellationToken
        );
        created!
            .RoleCapacities.Should()
            .BeEquivalentTo([
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Participant, 10, false),
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Leader, 1, false),
            ]);

        var storedCount = await Factory.QueryAsync(db =>
            Task.FromResult(db.ActivityRoleCapacities.Count(c => c.ActivityId == created.Id))
        );
        storedCount.Should().Be(2);
    }

    [Fact]
    public async Task Create_DuplicatedRoleCapacityRole_ReturnsBadRequest()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(
            thumb,
            roleCapacities:
            [
                new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 5),
                new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 9),
            ]
        );

        var response = await client.PostJsonAsync(
            $"/api/activities/{eventId}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.ActivityRoleCapacityDuplicated);
    }

    [Fact]
    public async Task Create_RoleCapacityWithoutPositiveCount_ReturnsValidationError()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var client = await LoginAsAdminAsync();
        var request = CreateRequest(
            thumb,
            roleCapacities:
            [
                new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 0),
            ]
        );

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
    public async Task Update_WithRoleCapacities_ReplacesExistingSet()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var id = await SeedActivityAsync(eventId, thumb);
        await Factory.SeedAsync(db =>
        {
            db.ActivityRoleCapacities.Add(
                new ActivityRoleCapacity
                {
                    ActivityId = id,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Leader,
                    DesiredCount = 1,
                }
            );
            db.ActivityRoleCapacities.Add(
                new ActivityRoleCapacity
                {
                    ActivityId = id,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                    DesiredCount = 5,
                }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();
        var request = new UpdateActivityRequest(
            "Despues",
            "Descripcion",
            "Sala",
            SeedIds.ActivityModalityTypes.Presencial,
            ActivityStart,
            ActivityEnd,
            thumb,
            [
                new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 3),
                new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Volunteer, 2),
            ]
        );

        var response = await client.PutJsonAsync(
            $"/api/activities/{id}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<ActivityResponse>(
            TestContext.Current.CancellationToken
        );
        updated!
            .RoleCapacities.Should()
            .BeEquivalentTo([
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Participant, 3, false),
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Volunteer, 2, false),
            ]);

        var stored = await Factory.QueryAsync(db =>
            Task.FromResult(
                db.ActivityRoleCapacities.Where(c => c.ActivityId == id)
                    .Select(c => new { c.ActivityRoleTypeId, c.DesiredCount })
                    .ToList()
            )
        );
        stored.Should().HaveCount(2);
        stored
            .Should()
            .ContainSingle(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant && c.DesiredCount == 3
            );
        stored
            .Should()
            .ContainSingle(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer && c.DesiredCount == 2
            );
    }

    [Fact]
    public async Task List_NonDeniedAssignmentsExceedDesiredCount_FlagsHighDemand()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = await SeedEventAsync(thumb);
        var crowded = await SeedActivityAsync(eventId, thumb, "Llena");
        var covered = await SeedActivityAsync(eventId, thumb, "Con hueco");
        await Factory.SeedAsync(db =>
        {
            db.ActivityRoleCapacities.Add(
                new ActivityRoleCapacity
                {
                    ActivityId = crowded,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                    DesiredCount = 1,
                }
            );
            db.ActivityRoleCapacities.Add(
                new ActivityRoleCapacity
                {
                    ActivityId = crowded,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Volunteer,
                    DesiredCount = 1,
                }
            );
            db.ActivityRoleCapacities.Add(
                new ActivityRoleCapacity
                {
                    ActivityId = covered,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                    DesiredCount = 2,
                }
            );
            foreach (var activityId in new[] { crowded, covered })
            {
                db.ActivityUserRoleAssignments.Add(
                    new ActivityUserRoleAssignment
                    {
                        UserId = TestSeedData.Users.MemberId,
                        ActivityId = activityId,
                        ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                        AssignmentStatusId = SeedIds.AssignmentStatusTypes.Confirmed,
                        CreatedAt = SignupStart,
                    }
                );
                db.ActivityUserRoleAssignments.Add(
                    new ActivityUserRoleAssignment
                    {
                        UserId = TestSeedData.Users.MemberChildId,
                        ActivityId = activityId,
                        ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                        AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
                        CreatedAt = SignupStart,
                    }
                );
                db.ActivityUserRoleAssignments.Add(
                    new ActivityUserRoleAssignment
                    {
                        UserId = TestSeedData.Users.PendingId,
                        ActivityId = activityId,
                        ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                        AssignmentStatusId = SeedIds.AssignmentStatusTypes.Denied,
                        CreatedAt = SignupStart,
                    }
                );
            }
            return Task.CompletedTask;
        });
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/activities?eventId={eventId}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ActivityResponse>>(
            TestContext.Current.CancellationToken
        );
        var crowdedCapacities = page!.Items.Single(a => a.Title == "Llena").RoleCapacities;
        crowdedCapacities
            .Single(c => c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant)
            .IsHighDemand.Should()
            .BeTrue();
        crowdedCapacities
            .Single(c => c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer)
            .IsHighDemand.Should()
            .BeFalse();
        page.Items.Single(a => a.Title == "Con hueco")
            .RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
            )
            .IsHighDemand.Should()
            .BeFalse();
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
    public async Task Update_AsAdmin_ChangesActivity()
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
            null
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
        roles!.Should().HaveCount(3);
        roles.Should().Contain(r => r.Id == SeedIds.ActivityRoleTypes.Leader && r.Name == "Líder");
        roles
            .Should()
            .Contain(r => r.Id == SeedIds.ActivityRoleTypes.Volunteer && r.Name == "Voluntario");
        roles
            .Should()
            .Contain(r =>
                r.Id == SeedIds.ActivityRoleTypes.Participant && r.Name == "Participante"
            );
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
}
