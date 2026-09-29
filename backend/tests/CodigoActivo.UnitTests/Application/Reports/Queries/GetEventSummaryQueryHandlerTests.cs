using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Reports.ReportTestData;

namespace CodigoActivo.UnitTests.Application.Reports.Queries;

public sealed class GetEventSummaryQueryHandlerTests
{
    private static readonly Guid BetaRoleId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GhostRoleId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid IdleRoleId = new("44444444-4444-4444-4444-444444444444");

    private readonly FakeReadStore store = new();
    private readonly GetEventSummaryQueryHandler sut;

    public GetEventSummaryQueryHandlerTests()
    {
        sut = new GetEventSummaryQueryHandler(store, new FakeQueryExecutor());
    }

    private static AssignmentRow SummaryAsg(
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

    private static ActivityRow SummaryActivity(Guid? eventId = null)
    {
        return new()
        {
            Title = "Actividad de prueba",
            Description = "Descripción de la actividad",
            Location = "Sala principal",
            EventId = eventId ?? Guid.Empty,
        };
    }

    private static ActivityRoleTypeRow Role(
        Guid id,
        string name,
        IEnumerable<AssignmentRow> assignments
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
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        store.Events.Add(
            new EventRow
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
        store.ReadsOf<AssignmentRow>().Should().Be(0);
        store.ReadsOf<ActivityRoleTypeRow>().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncMixedStatusesAndRepeatedUsersAggregatesCountsAndBreakdown()
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

        store.Events.Add(
            new EventRow
            {
                Subtitle = "Subtítulo del evento",
                Id = QueriedEventId,
                Title = "Feria",
                Activities = [SummaryActivity(), SummaryActivity()],
            }
        );
        store.Assignments.AddRange(assignments);
        store.ActivityRoleTypes.AddRange([
            Role(AlphaRoleId, "Alpha", assignments),
            Role(IdleRoleId, "Idle", assignments),
            Role(BetaRoleId, "Beta", assignments),
        ]);

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
    public async Task HandleAsyncNoAssignmentsReturnsZeroCounts()
    {
        store.Events.Add(
            new EventRow
            {
                Subtitle = "Subtítulo del evento",
                Id = QueriedEventId,
                Title = "Vacío",
            }
        );

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
