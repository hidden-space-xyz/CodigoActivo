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

            db.ActivityUserRoleAssignments.AddRange(
                Assignment(
                    ActivityAId,
                    TestSeedData.Users.MemberChildId,
                    SeedIds.ActivityRoleTypes.Volunteer,
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
        summary
            .RoleTypeBreakdown.Select(r => (r.RoleTypeId, r.RoleTypeName, r.ApprovedAssignments))
            .Should()
            .Equal(
                (SeedIds.ActivityRoleTypes.Leader, "Líder", 1),
                (SeedIds.ActivityRoleTypes.Participant, "Participante", 0),
                (SeedIds.ActivityRoleTypes.Volunteer, "Voluntario", 1)
            );
    }

    [Fact]
    public async Task EventSummary_RepeatedUserAcrossActivities_CountsDistinctVolunteersOnce()
    {
        await SeedEventGraphAsync();
        await Factory.SeedAsync(db =>
        {
            db.ActivityUserRoleAssignments.Add(
                Assignment(
                    ActivityAId,
                    TestSeedData.Users.AdminId,
                    SeedIds.ActivityRoleTypes.Volunteer,
                    SeedIds.AssignmentStatusTypes.Requested
                )
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/summary",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.ReadJsonAsync<EventSummaryResponse>(
            TestContext.Current.CancellationToken
        );
        summary!.TotalAssignments.Should().Be(5);
        summary.RequestedAssignments.Should().Be(2);
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
        var page = await response.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(4);
        page.Items.Select(a => a.FirstName).Should().Equal("Ada", "Bruno", "Mateo", "Pedro");

        var admin = page.Items.Single(a => a.UserId == TestSeedData.Users.AdminId);
        admin.Email.Should().Be(TestSeedData.AdminEmail);
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

        var child = page.Items.Single(a => a.UserId == TestSeedData.Users.MemberChildId);
        child.BirthDate.Should().Be(new DateOnly(2015, 5, 5));
        child.Email.Should().BeNull();
        child.UserTypeName.Should().Be("Participante");
        child.UserTypeColor.Should().Be("#3B82F6");
        child.Guardian.Should().NotBeNull();
        child.Guardian!.FirstName.Should().Be("Marta");
        child.Guardian.Email.Should().Be(TestSeedData.MemberEmail);
        child.Guardian.Phone.Should().Be("+34600000002");
        child.Assignments.Should().HaveCount(2);
        child.Assignments[0].ActivityId.Should().Be(ActivityAId);
        child.Assignments[0].HasTimeConflict.Should().BeTrue();
        child.Assignments[1].ActivityId.Should().Be(ActivityCId);
        child.Assignments[1].StatusName.Should().Be("Solicitada");
        child.Assignments[1].HasTimeConflict.Should().BeTrue();
    }

    [Fact]
    public async Task EventAttendees_StatusFilter_ReturnsUsersAndAssignmentsMatchingStatus()
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
            $"/api/reports/events/{EventId}/attendees?statusId={SeedIds.AssignmentStatusTypes.Confirmed}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(2);
        page.Items.Select(a => a.FirstName).Should().Equal("Ada", "Mateo");

        var child = page.Items.Single(a => a.UserId == TestSeedData.Users.MemberChildId);
        child.Assignments.Should().HaveCount(1);
        child.Assignments[0].ActivityId.Should().Be(ActivityAId);
        child.Assignments[0].StatusId.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        child.Assignments[0].HasTimeConflict.Should().BeTrue();
    }

    [Fact]
    public async Task EventAttendees_SortByEmail_OrdersByEmailWithNullsLast()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/attendees?sort=email",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Items.Select(a => a.FirstName).Should().Equal("Ada", "Bruno", "Pedro", "Mateo");
    }

    [Fact]
    public async Task EventAttendees_SortByBirthDateDescending_OrdersYoungestFirst()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/attendees?sort=-birthDate",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Items.Select(a => a.FirstName).Should().Equal("Mateo", "Pedro", "Bruno", "Ada");
    }

    [Fact]
    public async Task EventAttendees_SortByType_OrdersByUserTypeName()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/attendees?sort=type",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!
            .Items.Select(a => a.UserTypeName)
            .Should()
            .Equal("Participante", "Socio", "Socio", "Socio");
        page.Items.Select(a => a.FirstName).Should().Equal("Mateo", "Ada", "Pedro", "Bruno");
    }

    [Fact]
    public async Task EventAttendees_ActivityFilter_ComputesConflictFromHiddenAssignments()
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
            $"/api/reports/events/{EventId}/attendees?activityId={ActivityCId}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        var child = page
            .Items.Should()
            .ContainSingle(a => a.UserId == TestSeedData.Users.MemberChildId)
            .Subject;
        var assignment = child.Assignments.Should().ContainSingle().Subject;
        assignment.ActivityId.Should().Be(ActivityCId);
        assignment
            .HasTimeConflict.Should()
            .BeTrue("the overlapping assignment hidden by the filter still counts");
    }

    [Fact]
    public async Task EventAttendees_SearchFilter_MatchesGuardianData()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/attendees?search=marta",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Items.Single().UserId.Should().Be(TestSeedData.Users.MemberChildId);
    }

    [Fact]
    public async Task EventAttendees_PageSizeOne_PagesAttendeesKeepingTotal()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var firstResponse = await client.GetAsync(
            $"/api/reports/events/{EventId}/attendees?page=1&pageSize=1",
            TestContext.Current.CancellationToken
        );
        var secondResponse = await client.GetAsync(
            $"/api/reports/events/{EventId}/attendees?page=2&pageSize=1",
            TestContext.Current.CancellationToken
        );

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await firstResponse.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        first!.Total.Should().Be(4);
        first.Page.Should().Be(1);
        first.PageSize.Should().Be(1);
        first.Items.Should().ContainSingle(a => a.FirstName == "Ada");

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await secondResponse.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        second!.Total.Should().Be(4);
        second.Page.Should().Be(2);
        second.Items.Should().ContainSingle(a => a.FirstName == "Bruno");
    }

    [Fact]
    public async Task EventAttendees_MissingEvent_ReturnsEmptyPage()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{Guid.NewGuid()}/attendees",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventAttendeeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(0);
        page.Items.Should().BeEmpty();
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
    public async Task EventRoster_ExistingEvent_GroupsConfirmedParticipantsByActivity()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/roster",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.ReadJsonAsync<EventRosterResponse>(
            TestContext.Current.CancellationToken
        );
        report!.EventId.Should().Be(EventId);
        report.Title.Should().Be("Feria de Voluntariado");
        report.Activities.Should().HaveCount(2);
        report.Activities[0].ActivityId.Should().Be(ActivityBId);

        var charla = report.Activities.Single(a => a.ActivityId == ActivityBId);
        charla.Title.Should().Be("Charla");
        charla.Location.Should().Be("Sala");
        charla.Participants.Should().HaveCount(1);
        var admin = charla.Participants[0];
        admin.UserId.Should().Be(TestSeedData.Users.AdminId);
        admin.FirstName.Should().Be("Ada");
        admin.LastName.Should().Be("Admin");
        admin.BirthDate.Should().Be(new DateOnly(1985, 3, 12));
        admin.Email.Should().Be(TestSeedData.AdminEmail);
        admin.Phone.Should().Be("+34600000001");
        admin.RoleName.Should().Be("Líder");
        admin.Guardian.Should().BeNull();

        var taller = report.Activities.Single(a => a.ActivityId == ActivityAId);
        taller.Participants.Should().HaveCount(1);
        var child = taller.Participants[0];
        child.UserId.Should().Be(TestSeedData.Users.MemberChildId);
        child.FirstName.Should().Be("Mateo");
        child.BirthDate.Should().Be(new DateOnly(2015, 5, 5));
        child.Email.Should().BeNull();
        child.Phone.Should().BeNull();
        child.RoleName.Should().Be("Voluntario");
        child.Guardian.Should().NotBeNull();
        child.Guardian!.FirstName.Should().Be("Marta");
        child.Guardian.LastName.Should().Be("Miembro");
        child.Guardian.Email.Should().Be(TestSeedData.MemberEmail);
        child.Guardian.Phone.Should().Be("+34600000002");
    }

    [Fact]
    public async Task EventRoster_MissingEvent_ReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{Guid.NewGuid()}/roster",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task EventRoster_MemberUser_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            $"/api/reports/events/{EventId}/roster",
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
