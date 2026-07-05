using System.Net;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class ReportsControllerTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)
{
    private static readonly Guid EventId = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ActivityAId = new("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid ActivityBId = new("bbbbbbbb-0000-0000-0000-000000000002");

    private static readonly DateTimeOffset At = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

    private Task SeedEventGraphAsync()
    {
        return Factory.SeedAsync(db =>
        {
            db.Events.Add(new Event
            {
                Id = EventId,
                Title = "Feria de Voluntariado",
                Subtitle = "Edición 2026",
                Description = "{}",
                EventStartsAt = new DateOnly(2026, 5, 1),
                EventEndsAt = new DateOnly(2026, 5, 2),
                SignupStartsAt = At,
                SignupEndsAt = At,
                ThumbnailId = Guid.NewGuid(),
                CreatedAt = At,
                CreatedBy = TestSeedData.Users.AdminId,
            });

            db.Activities.Add(BuildActivity(ActivityAId, "Taller"));
            db.Activities.Add(BuildActivity(ActivityBId, "Charla"));

            db.ActivityAllowedRoleTypes.AddRange(
                Allowed(ActivityAId, SeedIds.ActivityRoleTypes.Leader),
                Allowed(ActivityAId, SeedIds.ActivityRoleTypes.Helper),
                Allowed(ActivityBId, SeedIds.ActivityRoleTypes.Leader),
                Allowed(ActivityBId, SeedIds.ActivityRoleTypes.Participant)
            );

            db.ActivityUserRoleAssignments.AddRange(
                Assignment(ActivityAId, TestSeedData.Users.MemberChildId, SeedIds.ActivityRoleTypes.Helper, SeedIds.AssignmentStatusTypes.Confirmed),
                Assignment(ActivityBId, TestSeedData.Users.AdminId, SeedIds.ActivityRoleTypes.Leader, SeedIds.AssignmentStatusTypes.Confirmed),
                Assignment(ActivityBId, TestSeedData.Users.PendingId, SeedIds.ActivityRoleTypes.Participant, SeedIds.AssignmentStatusTypes.Requested),
                Assignment(ActivityBId, TestSeedData.Users.BlockedId, SeedIds.ActivityRoleTypes.Leader, SeedIds.AssignmentStatusTypes.Denied)
            );

            return Task.CompletedTask;
        });
    }

    private static Activity BuildActivity(Guid id, string title) => new()
    {
        Id = id,
        Title = title,
        Description = "desc",
        Location = "Sala",
        ActivityStartsAt = At,
        ActivityEndsAt = At.AddHours(2),
        EventId = EventId,
        ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
        ThumbnailId = Guid.NewGuid(),
        CreatedAt = At,
        CreatedBy = TestSeedData.Users.AdminId,
    };

    private static ActivityAllowedRoleType Allowed(Guid activityId, Guid roleTypeId) =>
        new() { ActivityId = activityId, ActivityRoleTypeId = roleTypeId };

    private static ActivityUserRoleAssignment Assignment(Guid activityId, Guid userId, Guid roleTypeId, Guid statusId) =>
        new()
        {
            ActivityId = activityId,
            UserId = userId,
            ActivityRoleTypeId = roleTypeId,
            AssignmentStatusId = statusId,
        };

    [Fact]
    public async Task EventSummary_as_admin_returns_computed_aggregates()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/reports/events/{EventId}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.ReadJsonAsync<EventSummaryResponse>();
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
    public async Task EventSummary_missing_event_is_404_with_event_not_found()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/reports/events/{Guid.NewGuid()}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task EventAssignments_as_admin_lists_every_assignment_with_names()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/reports/events/{EventId}/assignments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.ReadJsonAsync<EventAssignmentsReportResponse>();
        report!.EventId.Should().Be(EventId);
        report.Title.Should().Be("Feria de Voluntariado");
        report.Items.Should().HaveCount(4);

        var confirmedLeader = report.Items.Single(i =>
            i.UserId == TestSeedData.Users.AdminId);
        confirmedLeader.ActivityId.Should().Be(ActivityBId);
        confirmedLeader.ActivityTitle.Should().Be("Charla");
        confirmedLeader.RoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Leader);
        confirmedLeader.RoleTypeName.Should().Be("Líder");
        confirmedLeader.StatusId.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        confirmedLeader.StatusName.Should().Be("Confirmada");
    }

    [Fact]
    public async Task ActivityAssignments_includes_signed_up_row_and_non_signed_up_parent()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/reports/activities/{ActivityAId}/assignments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.ReadJsonAsync<ActivityAssignmentsReportResponse>();
        report!.ActivityId.Should().Be(ActivityAId);
        report.Title.Should().Be("Taller");
        report.TotalSignups.Should().Be(1);
        report.Rows.Should().HaveCount(2);

        var child = report.Rows.Single(r => r.UserId == TestSeedData.Users.MemberChildId);
        child.SignedUp.Should().BeTrue();
        child.FirstName.Should().Be("Mateo");
        child.ParentId.Should().Be(TestSeedData.Users.MemberId);
        child.RoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Helper);
        child.RoleTypeName.Should().Be("Colaborador");
        child.StatusId.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        child.StatusName.Should().Be("Confirmada");

        var parent = report.Rows.Single(r => r.UserId == TestSeedData.Users.MemberId);
        parent.SignedUp.Should().BeFalse();
        parent.FirstName.Should().Be("Marta");
        parent.RoleTypeId.Should().BeNull();
        parent.RoleTypeName.Should().BeNull();
        parent.StatusId.Should().BeNull();
        parent.StatusName.Should().BeNull();
    }

    [Fact]
    public async Task ActivityAssignments_missing_activity_is_404_with_activity_not_found()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/reports/activities/{Guid.NewGuid()}/assignments");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task Dashboard_counts_reference_users_only_when_empty()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/reports/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await response.ReadJsonAsync<DashboardSummaryResponse>();
        dashboard!.Events.Should().Be(0);
        dashboard.Activities.Should().Be(0);
        dashboard.Resources.Should().Be(0);
        dashboard.Announcements.Should().Be(0);
        dashboard.Partners.Should().Be(0);
        dashboard.Users.Should().Be(5);
    }

    [Fact]
    public async Task Admin_only_endpoints_challenge_anonymous()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/reports/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_only_endpoints_forbid_members()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/reports/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
