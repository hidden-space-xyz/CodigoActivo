using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class ReportsControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly Guid EventId = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ActivityAId = new("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid ActivityBId = new("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid ActivityCId = new("bbbbbbbb-0000-0000-0000-000000000003");
    private static readonly Guid EventThumbnailId = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid ActivityAThumbnailId = new("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid ActivityBThumbnailId = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid ActivityCThumbnailId = new("cccccccc-0000-0000-0000-000000000004");

    private static readonly DateTimeOffset At = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

    private Task SeedEventGraphAsync()
    {
        return Factory.SeedAsync(db =>
        {
            db.Files.AddRange(
                Thumbnail(EventThumbnailId),
                Thumbnail(ActivityAThumbnailId),
                Thumbnail(ActivityBThumbnailId)
            );

            db.Events.Add(
                new Event
                {
                    Id = EventId,
                    Title = "Feria de Voluntariado",
                    Subtitle = "Edición 2026",
                    Description = "{}",
                    EventStartsAt = new DateOnly(2026, 5, 1),
                    EventEndsAt = new DateOnly(2026, 5, 2),
                    SignupStartsAt = At,
                    SignupEndsAt = At,
                    ThumbnailId = EventThumbnailId,
                    CreatedAt = At,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );

            db.Activities.Add(BuildActivity(ActivityAId, "Taller", ActivityAThumbnailId));
            db.Activities.Add(BuildActivity(ActivityBId, "Charla", ActivityBThumbnailId));

            db.ActivityAllowedRoleTypes.AddRange(
                Allowed(ActivityAId, SeedIds.ActivityRoleTypes.Leader),
                Allowed(ActivityAId, SeedIds.ActivityRoleTypes.Helper),
                Allowed(ActivityBId, SeedIds.ActivityRoleTypes.Leader),
                Allowed(ActivityBId, SeedIds.ActivityRoleTypes.Participant)
            );

            db.ActivityUserRoleAssignments.AddRange(
                Assignment(
                    ActivityAId,
                    TestSeedData.Users.MemberChildId,
                    SeedIds.ActivityRoleTypes.Helper,
                    SeedIds.AssignmentStatusTypes.Confirmed
                ),
                Assignment(
                    ActivityBId,
                    TestSeedData.Users.AdminId,
                    SeedIds.ActivityRoleTypes.Leader,
                    SeedIds.AssignmentStatusTypes.Confirmed
                ),
                Assignment(
                    ActivityBId,
                    TestSeedData.Users.PendingId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Requested
                ),
                Assignment(
                    ActivityBId,
                    TestSeedData.Users.BlockedId,
                    SeedIds.ActivityRoleTypes.Leader,
                    SeedIds.AssignmentStatusTypes.Denied
                )
            );

            return Task.CompletedTask;
        });
    }

    private static Activity BuildActivity(
        Guid id,
        string title,
        Guid thumbnailId,
        DateTimeOffset? startsAt = null
    ) =>
        new()
        {
            Id = id,
            Title = title,
            Description = "desc",
            Location = "Sala",
            ActivityStartsAt = startsAt ?? At,
            ActivityEndsAt = (startsAt ?? At).AddHours(2),
            EventId = EventId,
            ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
            ThumbnailId = thumbnailId,
            CreatedAt = At,
            CreatedBy = TestSeedData.Users.AdminId,
        };

    private static FileEntity Thumbnail(Guid id) =>
        new()
        {
            Id = id,
            Name = "thumb",
            Extension = "png",
            UploadedAt = At,
            UploadedBy = TestSeedData.Users.AdminId,
        };

    private static ActivityAllowedRoleType Allowed(Guid activityId, Guid roleTypeId) =>
        new() { ActivityId = activityId, ActivityRoleTypeId = roleTypeId };

    private static ActivityUserRoleAssignment Assignment(
        Guid activityId,
        Guid userId,
        Guid roleTypeId,
        Guid statusId
    ) =>
        new()
        {
            ActivityId = activityId,
            UserId = userId,
            ActivityRoleTypeId = roleTypeId,
            AssignmentStatusId = statusId,
            CreatedAt = At,
        };

    [Fact]
    public async Task EventSummary_ExistingEvent_ReturnsComputedAggregates()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/summary",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.ReadJsonAsync<EventSummaryResponse>(
            TestContext.Current.CancellationToken
        );
        summary!.EventId.Should().Be(EventId);
        summary.Title.Should().Be("Feria de Voluntariado");
        summary.ActivitiesCount.Should().Be(2);
        summary.TotalAssignments.Should().Be(4);
        summary.RequestedAssignments.Should().Be(1);
        summary.ConfirmedAssignments.Should().Be(2);
        summary.DeniedAssignments.Should().Be(1);
        summary.DistinctVolunteers.Should().Be(4);
    }

    [Fact]
    public async Task EventSummary_MissingEvent_ReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{Guid.NewGuid()}/summary",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task EventAttendees_ExistingEvent_GroupsAssignmentsPerAttendee()
    {
        await SeedEventGraphAsync();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(Thumbnail(ActivityCThumbnailId));
            db.Activities.Add(
                BuildActivity(ActivityCId, "Cierre", ActivityCThumbnailId, At.AddHours(1))
            );
            db.ActivityUserRoleAssignments.Add(
                Assignment(
                    ActivityCId,
                    TestSeedData.Users.MemberChildId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Requested
                )
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/attendees",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.ReadJsonAsync<EventAttendeesResponse>(
            TestContext.Current.CancellationToken
        );
        report!.EventId.Should().Be(EventId);
        report.Title.Should().Be("Feria de Voluntariado");
        report.Attendees.Should().HaveCount(4);

        var admin = report.Attendees.Single(a => a.UserId == TestSeedData.Users.AdminId);
        admin.FirstName.Should().NotBeNullOrWhiteSpace();
        admin.Email.Should().NotBeNullOrWhiteSpace();
        admin.UserTypeName.Should().Be("Socio");
        admin.UserTypeColor.Should().Be("#EF4444");
        admin.Guardian.Should().BeNull();
        admin.Assignments.Should().HaveCount(1);
        var assignment = admin.Assignments[0];
        assignment.ActivityId.Should().Be(ActivityBId);
        assignment.ActivityTitle.Should().Be("Charla");
        assignment.ActivityStartsAt.Should().Be(At);
        assignment.ActivityEndsAt.Should().Be(At.AddHours(2));
        assignment.RoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Leader);
        assignment.RoleTypeName.Should().Be("Líder");
        assignment.StatusId.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        assignment.StatusName.Should().Be("Confirmada");
        assignment.SignedUpAt.Should().Be(At);
        assignment.HasTimeConflict.Should().BeFalse();

        var child = report.Attendees.Single(a => a.UserId == TestSeedData.Users.MemberChildId);
        child.BirthDate.Should().Be(new DateOnly(2015, 5, 5));
        child.Email.Should().BeNull();
        child.UserTypeName.Should().Be("Participante");
        child.UserTypeColor.Should().Be("#3B82F6");
        child.Guardian.Should().NotBeNull();
        child.Guardian!.FirstName.Should().Be("Marta");
        child.Guardian.Email.Should().NotBeNullOrWhiteSpace();
        child.Guardian.Phone.Should().NotBeNullOrWhiteSpace();
        child.Assignments.Should().HaveCount(2);
        child.Assignments[0].ActivityId.Should().Be(ActivityAId);
        child.Assignments[0].HasTimeConflict.Should().BeTrue();
        child.Assignments[1].ActivityId.Should().Be(ActivityCId);
        child.Assignments[1].StatusName.Should().Be("Solicitada");
        child.Assignments[1].HasTimeConflict.Should().BeTrue();
    }

    [Fact]
    public async Task EventAttendees_MissingEvent_ReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{Guid.NewGuid()}/attendees",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task EventAttendees_MemberUser_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/attendees",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EventBadges_ExistingEvent_ReturnsConfirmedBadgesWithGuardianAndActivities()
    {
        await SeedEventGraphAsync();
        await Factory.SeedAsync(db =>
        {
            db.ActivityUserRoleAssignments.Add(
                Assignment(
                    ActivityBId,
                    TestSeedData.Users.MemberChildId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Confirmed
                )
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/badges",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.ReadJsonAsync<EventBadgesResponse>(
            TestContext.Current.CancellationToken
        );
        report!.EventId.Should().Be(EventId);
        report.Title.Should().Be("Feria de Voluntariado");

        report.Badges.Should().HaveCount(2);

        var admin = report.Badges[0];
        admin.UserId.Should().Be(TestSeedData.Users.AdminId);
        admin.FirstName.Should().Be("Ada");
        admin.LastName.Should().Be("Admin");
        admin.UserTypeName.Should().Be("Socio");
        admin.UserTypeColor.Should().Be("#EF4444");
        admin.Guardian.Should().BeNull();
        admin.Activities.Should().Equal("Charla");

        var child = report.Badges[1];
        child.UserId.Should().Be(TestSeedData.Users.MemberChildId);
        child.FirstName.Should().Be("Mateo");
        child.LastName.Should().Be("Miembro");
        child.UserTypeName.Should().Be("Participante");
        child.Guardian.Should().NotBeNull();
        child.Guardian!.FirstName.Should().Be("Marta");
        child.Guardian.LastName.Should().Be("Miembro");
        child.Guardian.Phone.Should().Be("+34600000002");
        child.Activities.Should().BeEquivalentTo("Taller", "Charla");
    }

    [Fact]
    public async Task EventBadges_MissingEvent_ReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{Guid.NewGuid()}/badges",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task EventBadges_MemberUser_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/badges",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_EmptyDatabase_CountsUsersOnly()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/reports/dashboard",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await response.ReadJsonAsync<DashboardSummaryResponse>(
            TestContext.Current.CancellationToken
        );
        dashboard!.Events.Should().Be(0);
        dashboard.Activities.Should().Be(0);
        dashboard.Resources.Should().Be(0);
        dashboard.Announcements.Should().Be(0);
        dashboard.Partners.Should().Be(0);
        dashboard.Users.Should().Be(5);
    }

    [Fact]
    public async Task Dashboard_AnonymousUser_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/reports/dashboard",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dashboard_MemberUser_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            "/api/reports/dashboard",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
