using System.Net;
using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class MeControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static FileEntity Thumbnail(Guid id) =>
        new()
        {
            Id = id,
            Name = "thumb",
            Extension = "png",
            UploadedAt = SeededAt,
            UploadedBy = TestSeedData.Users.AdminId,
        };

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
        var eventThumbnailId = Guid.NewGuid();
        var activityThumbnailId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.AddRange(Thumbnail(eventThumbnailId), Thumbnail(activityThumbnailId));
            db.Events.Add(
                new Event
                {
                    Id = eventId,
                    Title = "Evento",
                    Subtitle = "Sub",
                    Description = "{}",
                    EventStartsAt = new DateOnly(2026, 2, 1),
                    EventEndsAt = new DateOnly(2026, 2, 2),
                    SignupStartsAt = SeededAt,
                    SignupEndsAt = SeededAt.AddDays(10),
                    ThumbnailId = eventThumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Activities.Add(
                new Activity
                {
                    Id = activityId,
                    Title = activityTitle,
                    Description = "Descripción",
                    Location = "Sala",
                    ActivityStartsAt = activityStartsAt,
                    ActivityEndsAt = activityStartsAt.AddHours(2),
                    EventId = eventId,
                    ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                    ThumbnailId = activityThumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.ActivityUserRoleAssignments.Add(
                new ActivityUserRoleAssignment
                {
                    UserId = userId,
                    ActivityId = activityId,
                    ActivityRoleTypeId = roleTypeId,
                    AssignmentStatusId = statusId,
                }
            );
            return Task.CompletedTask;
        });
    }

    private static async Task<List<Application.DTOs.AssignedActivityResponse>> ReadAssignedAsync(
        HttpResponseMessage response
    )
    {
        return await response.ReadJsonAsync<List<Application.DTOs.AssignedActivityResponse>>(
                TestContext.Current.CancellationToken
            ) ?? [];
    }

    [Fact]
    public async Task AssignedActivities_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/me/assigned-activities",
            TestContext.Current.CancellationToken
        );

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

        var response = await client.GetAsync(
            "/api/me/assigned-activities",
            TestContext.Current.CancellationToken
        );

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
}
