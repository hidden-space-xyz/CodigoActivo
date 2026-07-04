using System.Net;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <c>GET /api/me/assigned-activities</c>: it is authenticated-only, scopes the
/// result to the caller's own assignments (identity from the session cookie), orders by activity start,
/// and projects the assignment's activity, role and status.
/// </summary>
public sealed class MeControllerTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private async Task SeedAssignmentAsync(
        Guid userId,
        string activityTitle,
        DateTimeOffset activityStartsAt,
        Guid roleTypeId,
        Guid statusId
    )
    {
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Events.Add(new Event
            {
                Id = eventId,
                Title = "Evento",
                Subtitle = "Sub",
                Description = "{}",
                EventStartsAt = new DateOnly(2026, 2, 1),
                EventEndsAt = new DateOnly(2026, 2, 2),
                SignupStartsAt = SeededAt,
                SignupEndsAt = SeededAt.AddDays(10),
                ThumbnailId = Guid.NewGuid(),
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            });
            db.Activities.Add(new Activity
            {
                Id = activityId,
                Title = activityTitle,
                Description = "Descripción",
                Location = "Sala",
                ActivityStartsAt = activityStartsAt,
                ActivityEndsAt = activityStartsAt.AddHours(2),
                EventId = eventId,
                ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                ThumbnailId = Guid.NewGuid(),
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            });
            db.ActivityUserRoleAssignments.Add(new ActivityUserRoleAssignment
            {
                UserId = userId,
                ActivityId = activityId,
                ActivityRoleTypeId = roleTypeId,
                AssignmentStatusId = statusId,
            });
            return Task.CompletedTask;
        });
    }

    private static async Task<List<CodigoActivo.Application.DTOs.AssignedActivityResponse>> ReadAssignedAsync(
        HttpResponseMessage response
    )
    {
        return await response.ReadJsonAsync<List<CodigoActivo.Application.DTOs.AssignedActivityResponse>>()
            ?? [];
    }

    [Fact]
    public async Task AssignedActivities_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/me/assigned-activities");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignedActivities_returns_the_callers_assignment_projected()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Taller de robótica",
            new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Leader,
            SeedIds.AssignmentStatusTypes.Confirmed
        );
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/me/assigned-activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadAssignedAsync(response);
        var assigned = items.Should().ContainSingle().Subject;
        assigned.Title.Should().Be("Taller de robótica");
        assigned.Description.Should().Be("Descripción");
        assigned.RoleType.Id.Should().Be(SeedIds.ActivityRoleTypes.Leader);
        assigned.RoleType.Name.Should().Be("Líder");
        assigned.Status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        assigned.Status.Name.Should().Be("Confirmada");
    }

    [Fact]
    public async Task AssignedActivities_returns_empty_list_when_caller_has_none()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/me/assigned-activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadAssignedAsync(response);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignedActivities_excludes_other_users_assignments()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberChildId,
            "Actividad ajena",
            new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Participant,
            SeedIds.AssignmentStatusTypes.Requested
        );
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/me/assigned-activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadAssignedAsync(response);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignedActivities_are_ordered_by_activity_start()
    {
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Segunda",
            new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Helper,
            SeedIds.AssignmentStatusTypes.Requested
        );
        await SeedAssignmentAsync(
            TestSeedData.Users.MemberId,
            "Primera",
            new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
            SeedIds.ActivityRoleTypes.Leader,
            SeedIds.AssignmentStatusTypes.Confirmed
        );
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/me/assigned-activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadAssignedAsync(response);
        items.Select(i => i.Title).Should().ContainInOrder("Primera", "Segunda");
    }
}
