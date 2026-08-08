using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Reports.ReportTestData;

namespace CodigoActivo.UnitTests.Application.Reports.Queries;

public sealed class GetEventSummaryQueryHandlerTests
{
    private static readonly Guid BetaRoleId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GhostRoleId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid IdleRoleId = new("44444444-4444-4444-4444-444444444444");

    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly GetEventSummaryQueryHandler sut;

    public GetEventSummaryQueryHandlerTests()
    {
        sut = new GetEventSummaryQueryHandler(
            events,
            roleTypes,
            activities,
            new FakeQueryExecutor()
        );
    }

    private void HasRoleTypes(params ActivityRoleType[] list)
    {
        roleTypes.Query().Returns(list.AsQueryable());
    }

    private static ActivityUserRoleAssignment SummaryAsg(
        Guid userId,
        Guid roleId,
        Guid statusId,
        Guid? eventId = null
    )
    {
        return new()
        {
            UserId = userId,
            ActivityId = Guid.NewGuid(),
            Activity = SummaryActivity(eventId ?? QueriedEventId),
            ActivityRoleTypeId = roleId,
            AssignmentStatusId = statusId,
        };
    }

    private static Activity SummaryActivity(Guid? eventId = null)
    {
        return new()
        {
            Title = "Actividad de prueba",
            Description = "Descripción de la actividad",
            Location = "Sala principal",
            EventId = eventId ?? Guid.Empty,
        };
    }

    private static ActivityRoleType Role(
        Guid id,
        string name,
        IEnumerable<ActivityUserRoleAssignment> assignments
    )
    {
        return new()
        {
            Description = "Descripción de prueba",
            Id = id,
            Name = name,
            Assignments = [.. assignments.Where(a => a.ActivityRoleTypeId == id)],
        };
    }

    [Fact]
    public async Task HandleAsync_EventMissing_ReturnsNotFound()
    {
        events.HasEvents(
            new Event
            {
                Subtitle = "Subtítulo del evento",
                Id = Guid.NewGuid(),
                Title = "Otra",
            }
        );

        var result = await sut.HandleAsync(
            new GetEventSummaryQuery(QueriedEventId),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        activities.DidNotReceive().QueryAssignments();
        roleTypes.DidNotReceive().Query();
    }

    [Fact]
    public async Task HandleAsync_MixedStatusesAndRepeatedUsers_AggregatesCountsAndBreakdown()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        var assignments = new[]
        {
            SummaryAsg(user1, AlphaRoleId, Confirmed),
            SummaryAsg(user2, AlphaRoleId, Confirmed),
            SummaryAsg(user1, BetaRoleId, Confirmed),
            SummaryAsg(user3, BetaRoleId, Requested),
            SummaryAsg(user2, GhostRoleId, Denied),
            SummaryAsg(user1, AlphaRoleId, Confirmed, Guid.NewGuid()),
        };

        events.HasEvents(
            new Event
            {
                Subtitle = "Subtítulo del evento",
                Id = QueriedEventId,
                Title = "Feria",
                Activities = [SummaryActivity(), SummaryActivity()],
            }
        );
        activities.HasAssignments(assignments);
        HasRoleTypes(
            Role(AlphaRoleId, "Alpha", assignments),
            Role(IdleRoleId, "Idle", assignments),
            Role(BetaRoleId, "Beta", assignments)
        );

        var result = await sut.HandleAsync(
            new GetEventSummaryQuery(QueriedEventId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var summary = result.Value;
        summary.EventId.Should().Be(QueriedEventId);
        summary.Title.Should().Be("Feria");
        summary.ActivitiesCount.Should().Be(2);
        summary.TotalAssignments.Should().Be(5);
        summary.RequestedAssignments.Should().Be(1);
        summary.ConfirmedAssignments.Should().Be(3);
        summary.DeniedAssignments.Should().Be(1);
        summary.DistinctVolunteers.Should().Be(3);
        summary
            .RoleTypeBreakdown.Should()
            .Equal(
                new EventRoleTypeSummaryResponse(AlphaRoleId, "Alpha", 2),
                new EventRoleTypeSummaryResponse(BetaRoleId, "Beta", 1),
                new EventRoleTypeSummaryResponse(IdleRoleId, "Idle", 0)
            );
        summary.RoleTypeBreakdown.Should().NotContain(r => r.RoleTypeId == GhostRoleId);
    }

    [Fact]
    public async Task HandleAsync_NoAssignments_ReturnsZeroCounts()
    {
        events.HasEvents(
            new Event
            {
                Subtitle = "Subtítulo del evento",
                Id = QueriedEventId,
                Title = "Vacío",
            }
        );
        activities.HasAssignments();
        HasRoleTypes();

        var result = await sut.HandleAsync(
            new GetEventSummaryQuery(QueriedEventId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.ActivitiesCount.Should().Be(0);
        result.Value.TotalAssignments.Should().Be(0);
        result.Value.RequestedAssignments.Should().Be(0);
        result.Value.ConfirmedAssignments.Should().Be(0);
        result.Value.DeniedAssignments.Should().Be(0);
        result.Value.DistinctVolunteers.Should().Be(0);
        result.Value.RoleTypeBreakdown.Should().BeEmpty();
    }
}
