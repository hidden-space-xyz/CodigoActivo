using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class ActivitiesAssignmentTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset OpenSignupStart = new(
        2026,
        7,
        1,
        0,
        0,
        0,
        TimeSpan.Zero
    );
    private static readonly DateTimeOffset OpenSignupEnd = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedSignupStart = new(
        2026,
        6,
        1,
        0,
        0,
        0,
        TimeSpan.Zero
    );
    private static readonly DateTimeOffset ClosedSignupEnd = new(
        2026,
        6,
        30,
        0,
        0,
        0,
        TimeSpan.Zero
    );
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

    private async Task<(Guid EventId, Guid ActivityId)> SeedActivityAsync(
        bool openSignup = true,
        DateTimeOffset? activityStart = null,
        DateTimeOffset? activityEnd = null
    )
    {
        var thumb = await SeedThumbnailAsync();
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
                    EventStartsAt = new DateOnly(2026, 7, 1),
                    EventEndsAt = new DateOnly(2026, 7, 31),
                    SignupStartsAt = openSignup ? OpenSignupStart : ClosedSignupStart,
                    SignupEndsAt = openSignup ? OpenSignupEnd : ClosedSignupEnd,
                    ThumbnailId = thumb,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Activities.Add(
                new Activity
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
                }
            );
            return Task.CompletedTask;
        });
        return (eventId, activityId);
    }

    private Task SeedAssignmentAsync(
        Guid activityId,
        Guid userId,
        Guid? roleId = null,
        Guid? statusId = null
    )
    {
        return Factory.SeedAsync(db =>
        {
            db.ActivityUserRoleAssignments.Add(
                new ActivityUserRoleAssignment
                {
                    ActivityId = activityId,
                    UserId = userId,
                    ActivityRoleTypeId = roleId ?? SeedIds.ActivityRoleTypes.Leader,
                    AssignmentStatusId = statusId ?? SeedIds.AssignmentStatusTypes.Requested,
                }
            );
            return Task.CompletedTask;
        });
    }

    private Task<ActivityUserRoleAssignment?> FindAssignmentAsync(Guid activityId, Guid userId)
    {
        return Factory.QueryAsync(db =>
            db.ActivityUserRoleAssignments.FirstOrDefaultAsync(
                a => a.ActivityId == activityId && a.UserId == userId,
                Ct
            )
        );
    }

    [Fact]
    public async Task Assign_SelfMember_PersistsRequestedAssignment()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignRequest(SeedIds.ActivityRoleTypes.Leader);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/assign",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<AssignmentResponse>(Ct);
        body!.UserId.Should().Be(TestSeedData.Users.MemberId);
        body.Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Requested);
        var stored = await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        stored!.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Leader);
    }

    [Fact]
    public async Task Assign_ActivityMissing_ReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();
        var request = new AssignRequest(SeedIds.ActivityRoleTypes.Leader);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{Guid.NewGuid()}/{TestSeedData.Users.MemberId}/assign",
            request,
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task Assign_ChildAsLeader_ReturnsBadRequest()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignRequest(SeedIds.ActivityRoleTypes.Leader);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberChildId}/assign",
            request,
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.ActivityRoleNotAllowed);
        (await FindAssignmentAsync(activityId, TestSeedData.Users.MemberChildId)).Should().BeNull();
    }

    [Fact]
    public async Task Assign_NonHouseholdUser_ReturnsForbidden()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignRequest(SeedIds.ActivityRoleTypes.Leader);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.BlockedId}/assign",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignHousehold_SelfAndChild_CreatesBothAssignments()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignHouseholdRequest([
            new(TestSeedData.Users.MemberId, SeedIds.ActivityRoleTypes.Leader),
            new(TestSeedData.Users.MemberChildId, SeedIds.ActivityRoleTypes.Participant),
        ]);

        var response = await client.PostJsonAsync(
            $"/api/activities/{activityId}/assign-household",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<IReadOnlyList<AssignmentResponse>>(Ct);
        created!.Should().HaveCount(2);
        var member = await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        member!.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Leader);
        var child = await FindAssignmentAsync(activityId, TestSeedData.Users.MemberChildId);
        child!.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Participant);
    }

    [Fact]
    public async Task AssignHousehold_SelfAndChild_EmailsOneSignupSummaryToTheMember()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignHouseholdRequest([
            new(TestSeedData.Users.MemberId, SeedIds.ActivityRoleTypes.Leader),
            new(TestSeedData.Users.MemberChildId, SeedIds.ActivityRoleTypes.Participant),
        ]);

        var response = await client.PostJsonAsync(
            $"/api/activities/{activityId}/assign-household",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var message = Factory.EmailSender.Sent.Should().ContainSingle().Which;
        message.ToAddress.Should().Be(TestSeedData.MemberEmail);
        message.Subject.Should().Be("Inscripción recibida: Actividad");
        message
            .TextBody.Should()
            .Contain("Marta Miembro (Líder)")
            .And.Contain("Mateo Miembro (Participante)")
            .And.Contain("Evento");
    }

    [Fact]
    public async Task ChangeStatus_ConfirmedForDependentMinor_EmailsTheDecisionToTheGuardian()
    {
        var (_, activityId) = await SeedActivityAsync();
        await SeedAssignmentAsync(
            activityId,
            TestSeedData.Users.MemberChildId,
            SeedIds.ActivityRoleTypes.Participant
        );
        var client = await LoginAsAdminAsync();
        var request = new ChangeAssignmentStatusRequest(SeedIds.AssignmentStatusTypes.Confirmed);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberChildId}/change-status",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var message = Factory.EmailSender.Sent.Should().ContainSingle().Which;
        message.ToAddress.Should().Be(TestSeedData.MemberEmail);
        message.Subject.Should().Be("Inscripción confirmada: Actividad");
        message
            .TextBody.Should()
            .Contain("la inscripción de Mateo Miembro")
            .And.Contain("Participa como: Participante");
    }

    [Fact]
    public async Task Unassign_SelfMemberSignupOpen_RemovesAssignment()
    {
        var (_, activityId) = await SeedActivityAsync();
        await SeedAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        var client = await LoginAsMemberAsync();

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/unassign",
            ct: Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId)).Should().BeNull();
    }

    [Fact]
    public async Task ChangeStatus_Admin_UpdatesAndPersistsStatus()
    {
        var (_, activityId) = await SeedActivityAsync();
        await SeedAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        var client = await LoginAsAdminAsync();
        var request = new ChangeAssignmentStatusRequest(SeedIds.AssignmentStatusTypes.Confirmed);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/change-status",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<AssignmentResponse>(Ct);
        body!.Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        var stored = await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        stored!.AssignmentStatusId.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
    }

    [Fact]
    public async Task ChangeRole_Admin_UpdatesAndPersistsRole()
    {
        var (_, activityId) = await SeedActivityAsync();
        await SeedAssignmentAsync(
            activityId,
            TestSeedData.Users.MemberId,
            SeedIds.ActivityRoleTypes.Leader
        );
        var client = await LoginAsAdminAsync();
        var request = new ChangeAssignmentRoleRequest(SeedIds.ActivityRoleTypes.Volunteer);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/change-role",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<AssignmentResponse>(Ct);
        body!.RoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Volunteer);
        var stored = await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        stored!.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Volunteer);
    }

    [Fact]
    public async Task ChangeRole_AdminSetsLeaderForParticipantTypeUser_UpdatesRole()
    {
        var (_, activityId) = await SeedActivityAsync();
        await SeedAssignmentAsync(
            activityId,
            TestSeedData.Users.MemberChildId,
            SeedIds.ActivityRoleTypes.Participant
        );
        var client = await LoginAsAdminAsync();
        var request = new ChangeAssignmentRoleRequest(SeedIds.ActivityRoleTypes.Leader);

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberChildId}/change-role",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAssignmentAsync(activityId, TestSeedData.Users.MemberChildId);
        stored!.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Leader);
    }

    [Fact]
    public async Task Overlaps_ConflictingActivityForSelf_ReturnsOverlap()
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
            TestUri.Rel($"/api/activities/{targetId}/overlaps/{TestSeedData.Users.MemberId}"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<TimeOverlapResponse>(Ct);
        body!.HasOverlaps.Should().BeTrue();
        body.Overlaps.Should().ContainSingle(o => o.ActivityId == otherId);
    }

    [Fact]
    public async Task HouseholdAssignments_MemberAndChildrenAssigned_ReturnsBothOrderedByFirstName()
    {
        var (eventId, activityId) = await SeedActivityAsync();
        await SeedAssignmentAsync(activityId, TestSeedData.Users.MemberChildId);
        await SeedAssignmentAsync(activityId, TestSeedData.Users.MemberId);
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/activities/household-assignments/{eventId}"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<IReadOnlyList<HouseholdMemberAssignmentResponse>>(
            Ct
        );
        body!
            .Select(a => a.UserId)
            .Should()
            .Equal(TestSeedData.Users.MemberId, TestSeedData.Users.MemberChildId);
        body!.Select(a => a.FirstName).Should().Equal("Marta", "Mateo");
    }

    [Fact]
    public async Task HouseholdAssignments_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/activities/household-assignments/{Guid.NewGuid()}"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assign_UnknownRoleType_ReturnsBadRequest()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignRequest(Guid.NewGuid());

        var response = await client.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/assign",
            request,
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.ActivityRoleNotAllowed);
        (await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId)).Should().BeNull();
    }

    [Fact]
    public async Task AssignHousehold_ChildAsLeader_ReturnsBadRequest()
    {
        var (_, activityId) = await SeedActivityAsync();
        var client = await LoginAsMemberAsync();
        var request = new AssignHouseholdRequest([
            new(TestSeedData.Users.MemberId, SeedIds.ActivityRoleTypes.Leader),
            new(TestSeedData.Users.MemberChildId, SeedIds.ActivityRoleTypes.Leader),
        ]);

        var response = await client.PostJsonAsync(
            $"/api/activities/{activityId}/assign-household",
            request,
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.ActivityRoleNotAllowed);
        (await FindAssignmentAsync(activityId, TestSeedData.Users.MemberId)).Should().BeNull();
        (await FindAssignmentAsync(activityId, TestSeedData.Users.MemberChildId)).Should().BeNull();
    }

    [Fact]
    public async Task SignupRoles_MemberWithChild_ReturnsRolesPerHouseholdMember()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/activities/signup-roles"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<IReadOnlyList<HouseholdSignupRolesResponse>>(Ct);
        body.Should().HaveCount(2);
        var self = body.Single(m => m.UserId == TestSeedData.Users.MemberId);
        self.Roles.Select(r => r.Id)
            .Should()
            .Equal(
                SeedIds.ActivityRoleTypes.Participant,
                SeedIds.ActivityRoleTypes.Volunteer,
                SeedIds.ActivityRoleTypes.Leader
            );
        self.Roles.Select(r => r.Name).Should().Equal("Participante", "Voluntario", "Líder");
        var child = body.Single(m => m.UserId == TestSeedData.Users.MemberChildId);
        child
            .Roles.Select(r => r.Id)
            .Should()
            .Equal(SeedIds.ActivityRoleTypes.Participant, SeedIds.ActivityRoleTypes.Volunteer);
        child.Roles.Select(r => r.Name).Should().Equal("Participante", "Voluntario");
    }

    [Fact]
    public async Task SignupRoles_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/activities/signup-roles"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
