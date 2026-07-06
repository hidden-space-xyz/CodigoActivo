using System.Net;
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

public sealed class ActivitiesAssignmentTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset OpenSignupStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OpenSignupEnd = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedSignupStart = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedSignupEnd = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
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

    private async Task<(Guid EventId, Guid ActivityId)> SeedActivityAsync(
        bool openSignup = true,
        DateTimeOffset? activityStart = null,
        DateTimeOffset? activityEnd = null,
        params Guid[] allowedRoles
    )
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var roles = allowedRoles.Length == 0 ? [SeedIds.ActivityRoleTypes.Leader] : allowedRoles;
        await Factory.SeedAsync(db =>
        {
            db.Events.Add(new Event
            {
                Id = eventId,
                Title = "Evento",
                Subtitle = "Sub",
                EventStartsAt = new DateOnly(2026, 7, 1),
                EventEndsAt = new DateOnly(2026, 7, 31),
                SignupStartsAt = openSignup ? OpenSignupStart : ClosedSignupStart,
                SignupEndsAt = openSignup ? OpenSignupEnd : ClosedSignupEnd,
                ThumbnailId = thumb,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = TestSeedData.Users.AdminId,
            });
            db.Activities.Add(new Activity
            {
                Id = activityId,
                Title = "Actividad",
                Description = "Descripcion",
                Location = "Sala",
                ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                ActivityStartsAt = activityStart ?? ActivityStart,
                ActivityEndsAt = activityEnd ?? ActivityEnd,
                EventId = eventId,
                ThumbnailId = thumb,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = TestSeedData.Users.AdminId,
                AllowedRoleTypes = roles
                    .Select(r => new ActivityAllowedRoleType { ActivityId = activityId, ActivityRoleTypeId = r })
                    .ToList(),
            });
            return Task.CompletedTask;
        });
        return (eventId, activityId);
    }

    private Task SeedAssignmentAsync(Guid activityId, Guid userId, Guid? roleId = null, Guid? statusId = null) =>
        Factory.SeedAsync(db =>
        {
            db.ActivityUserRoleAssignments.Add(new ActivityUserRoleAssignment
            {
                ActivityId = activityId,
                UserId = userId,
                ActivityRoleTypeId = roleId ?? SeedIds.ActivityRoleTypes.Leader,
                AssignmentStatusId = statusId ?? SeedIds.AssignmentStatusTypes.Requested,
            });
            return Task.CompletedTask;
        });

    private Task<ActivityUserRoleAssignment?> FindAssignmentAsync(Guid activityId, Guid userId) =>
        Factory.QueryAsync(db =>
            db.ActivityUserRoleAssignments
                .FirstOrDefaultAsync(a => a.ActivityId == activityId && a.UserId == userId));

    [Fact]
    public async Task Assign_as_self_member_persists_requested_assignment()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignRequest(SeedIds.ActivityRoleTypes.Leader);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/assign",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<AssignmentResponse>();
        body!.UserId.Should().Be(TestSeedData.Users.MemberId);
        body.Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Requested);
        var stored = await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        stored!.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Leader);
    }

    [Fact]
    public async Task Assign_missing_activity_is_404()
    {
        var client = await LoginAsAdminAsync();
        var request = new AssignRequest(SeedIds.ActivityRoleTypes.Leader);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{Guid.NewGuid()}/{TestSeedData.Users.MemberId}/assign",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task Assign_for_a_non_household_user_is_forbidden()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignRequest(SeedIds.ActivityRoleTypes.Leader);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.BlockedId}/assign",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignHousehold_creates_for_self_and_child()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignHouseholdRequest(
        [
            new(TestSeedData.Users.MemberId, SeedIds.ActivityRoleTypes.Leader),
            new(TestSeedData.Users.MemberChildId, SeedIds.ActivityRoleTypes.Leader),
        ]);

        var response = await client.PostJsonAsync(
            $"/api/activities/{activityId}/assign-household",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<IReadOnlyList<AssignmentResponse>>();
        created!.Should().HaveCount(2);
        (await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId)).Should().NotBeNull();
        (await FindAssignmentAsync(activityId, TestSeedData.Users.MemberChildId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Unassign_as_self_member_when_open_removes_assignment()
    {
        var (_, activityId) = await SeedActivityAsync();
        await SeedAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        var client = await LoginAsMemberAsync();

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/unassign"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId)).Should().BeNull();
    }

    [Fact]
    public async Task ChangeStatus_as_admin_updates_and_persists()
    {
        var (_, activityId) = await SeedActivityAsync();
        await SeedAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        var client = await LoginAsAdminAsync();
        var request = new ChangeAssignmentStatusRequest(SeedIds.AssignmentStatusTypes.Confirmed);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/change-status",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<AssignmentResponse>();
        body!.Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        var stored = await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        stored!.AssignmentStatusId.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
    }

    [Fact]
    public async Task ChangeRole_as_admin_updates_and_persists()
    {
        var (_, activityId) = await SeedActivityAsync(
            allowedRoles: [SeedIds.ActivityRoleTypes.Leader, SeedIds.ActivityRoleTypes.Helper]
        );
        await SeedAssignmentAsync(activityId, TestSeedData.Users.MemberId, SeedIds.ActivityRoleTypes.Leader);
        var client = await LoginAsAdminAsync();
        var request = new ChangeAssignmentRoleRequest(SeedIds.ActivityRoleTypes.Helper);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/change-role",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<AssignmentResponse>();
        body!.RoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Helper);
        var stored = await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        stored!.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Helper);
    }

    [Fact]
    public async Task Overlaps_reports_conflicting_activity_for_self()
    {
        var (_, targetId) = await SeedActivityAsync(
            activityStart: new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            activityEnd: new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)
        );
        var (_, otherId) = await SeedActivityAsync(
            activityStart: new DateTimeOffset(2026, 7, 10, 11, 0, 0, TimeSpan.Zero),
            activityEnd: new DateTimeOffset(2026, 7, 10, 13, 0, 0, TimeSpan.Zero)
        );
        await SeedAssignmentAsync(otherId, TestSeedData.Users.MemberId);
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            $"/api/activities/{targetId}/overlaps/{TestSeedData.Users.MemberId}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<TimeOverlapResponse>();
        body!.HasOverlaps.Should().BeTrue();
        body.Overlaps.Should().ContainSingle(o => o.ActivityId == otherId);
    }

    [Fact]
    public async Task HouseholdAssignments_returns_member_and_children()
    {
        var (eventId, activityId) = await SeedActivityAsync();
        await SeedAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        await SeedAssignmentAsync(activityId, TestSeedData.Users.MemberChildId);
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync($"/api/activities/household-assignments/{eventId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<IReadOnlyList<HouseholdMemberAssignmentResponse>>();
        body!.Should().HaveCount(2);
        body.Should().Contain(a => a.UserId == TestSeedData.Users.MemberId);
        body.Should().Contain(a => a.UserId == TestSeedData.Users.MemberChildId);
    }

    [Fact]
    public async Task HouseholdAssignments_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync($"/api/activities/household-assignments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
