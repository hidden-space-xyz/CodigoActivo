using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class VerifyTimeOverlapsQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly VerifyTimeOverlapsQueryHandler sut;

    public VerifyTimeOverlapsQueryHandlerTests()
    {
        sut = new VerifyTimeOverlapsQueryHandler(
            store,
            new FakeQueryExecutor(),
            new ActingUserPolicy(currentUser, users)
        );
    }

    private static AssignmentRow OverlapAssignment(Guid userId, ActivityRow activity)
    {
        return new()
        {
            UserId = userId,
            ActivityId = activity.Id,
            Activity = activity,
        };
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(ActivityId.New(), UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task HandleAsyncOverlappingAssignmentsReportsOverlapsExcludingTargetAndOtherUsers()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivityRow(Guid.NewGuid(), 10, 12);
        var clash = OverlapActivityRow(Guid.NewGuid(), 11, 13, "Choque");
        store.Activities.Add(target);
        store.Assignments.AddRange([
            OverlapAssignment(userId, target),
            OverlapAssignment(userId, clash),
            OverlapAssignment(Guid.NewGuid(), OverlapActivityRow(Guid.NewGuid(), 11, 13, "Ajeno")),
        ]);

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(ActivityId.From(target.Id), UserId.From(userId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeTrue();
        result.Value.Overlaps.Should().ContainSingle();
        result.Value.Overlaps[0].ActivityId.Should().Be(clash.Id);
        result.Value.Overlaps[0].Title.Should().Be("Choque");
    }

    [Fact]
    public async Task HandleAsyncMultipleOverlapsOrdersByStartThenActivityId()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivityRow(Guid.NewGuid(), 9, 14);
        var earliest = OverlapActivityRow(Guid.NewGuid(), 10, 11);
        var tieFirst = OverlapActivityRow(new Guid("00000000-0000-0000-0000-000000000001"), 11, 12);
        var tieSecond = OverlapActivityRow(
            new Guid("00000000-0000-0000-0000-000000000002"),
            11,
            12
        );
        store.Activities.Add(target);
        store.Assignments.AddRange([
            OverlapAssignment(userId, tieSecond),
            OverlapAssignment(userId, tieFirst),
            OverlapAssignment(userId, earliest),
        ]);

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(ActivityId.From(target.Id), UserId.From(userId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Overlaps.Select(o => o.ActivityId)
            .Should()
            .Equal(earliest.Id, tieFirst.Id, tieSecond.Id);
    }

    [Fact]
    public async Task HandleAsyncDisjointAssignmentsReportsNoOverlaps()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivityRow(Guid.NewGuid(), 10, 12);
        store.Activities.Add(target);
        store.Assignments.Add(
            OverlapAssignment(userId, OverlapActivityRow(Guid.NewGuid(), 13, 14))
        );

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(ActivityId.From(target.Id), UserId.From(userId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeFalse();
        result.Value.Overlaps.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAdjacentAssignmentsReportsNoOverlaps()
    {
        var userId = Guid.NewGuid();
        var target = OverlapActivityRow(Guid.NewGuid(), 10, 12);
        store.Activities.Add(target);
        store.Assignments.Add(
            OverlapAssignment(userId, OverlapActivityRow(Guid.NewGuid(), 12, 14))
        );

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(ActivityId.From(target.Id), UserId.From(userId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.HasOverlaps.Should().BeFalse();
        result.Value.Overlaps.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAnotherUsersAgendaReturnsForbidden()
    {
        currentUser.IsAdmin = false;
        var target = OverlapActivityRow(Guid.NewGuid(), 10, 12);
        store.Activities.Add(target);

        var result = await sut.HandleAsync(
            new VerifyTimeOverlapsQuery(ActivityId.From(target.Id), UserId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActingForAnotherUserForbidden);
    }
}
