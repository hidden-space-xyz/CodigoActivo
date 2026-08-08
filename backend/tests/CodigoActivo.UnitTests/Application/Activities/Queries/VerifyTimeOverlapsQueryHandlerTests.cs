using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class VerifyTimeOverlapsQueryHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly VerifyTimeOverlapsQueryHandler sut;

    public VerifyTimeOverlapsQueryHandlerTests()
    {
        sut = new VerifyTimeOverlapsQueryHandler(activities, new FakeQueryExecutor());
    }

    private static ActivityUserRoleAssignment OverlapAssignment(Guid userId, Activity activity)
    {
        return new()
        {
            UserId = userId,
            ActivityId = activity.Id,
            Activity = activity,
        };
    }

    [Fact]
    public async Task HandleAsync_ActivityMissing_ReturnsNotFound()
    {
        activities.Query().Returns(new List<Activity>().AsQueryable());

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task HandleAsync_OverlappingAssignments_ReportsOverlapsExcludingTargetAndOtherUsers()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivity(Guid.NewGuid(), 10, 12);
        var clash = OverlapActivity(Guid.NewGuid(), 11, 13, "Choque");
        activities.Query().Returns(new List<Activity> { target }.AsQueryable());
        activities.HasAssignments(
            OverlapAssignment(userId, target),
            OverlapAssignment(userId, clash),
            OverlapAssignment(Guid.NewGuid(), OverlapActivity(Guid.NewGuid(), 11, 13, "Ajeno"))
        );

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(target.Id, userId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeTrue();
        result.Value.Overlaps.Should().ContainSingle();
        result.Value.Overlaps[0].ActivityId.Should().Be(clash.Id);
        result.Value.Overlaps[0].Title.Should().Be("Choque");
    }

    [Fact]
    public async Task HandleAsync_MultipleOverlaps_OrdersByStartThenActivityId()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivity(Guid.NewGuid(), 9, 14);
        var earliest = OverlapActivity(Guid.NewGuid(), 10, 11);
        var tieFirst = OverlapActivity(new Guid("00000000-0000-0000-0000-000000000001"), 11, 12);
        var tieSecond = OverlapActivity(new Guid("00000000-0000-0000-0000-000000000002"), 11, 12);
        activities.Query().Returns(new List<Activity> { target }.AsQueryable());
        activities.HasAssignments(
            OverlapAssignment(userId, tieSecond),
            OverlapAssignment(userId, tieFirst),
            OverlapAssignment(userId, earliest)
        );

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(target.Id, userId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Overlaps.Select(o => o.ActivityId)
            .Should()
            .Equal(earliest.Id, tieFirst.Id, tieSecond.Id);
    }

    [Fact]
    public async Task HandleAsync_DisjointAssignments_ReportsNoOverlaps()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivity(Guid.NewGuid(), 10, 12);
        activities.Query().Returns(new List<Activity> { target }.AsQueryable());
        activities.HasAssignments(
            OverlapAssignment(userId, OverlapActivity(Guid.NewGuid(), 13, 14))
        );

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(target.Id, userId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeFalse();
        result.Value.Overlaps.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_AdjacentAssignments_ReportsNoOverlaps()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivity(Guid.NewGuid(), 10, 12);
        activities.Query().Returns(new List<Activity> { target }.AsQueryable());
        activities.HasAssignments(
            OverlapAssignment(userId, OverlapActivity(Guid.NewGuid(), 12, 14))
        );

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(target.Id, userId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeFalse();
        result.Value.Overlaps.Should().BeEmpty();
    }
}
