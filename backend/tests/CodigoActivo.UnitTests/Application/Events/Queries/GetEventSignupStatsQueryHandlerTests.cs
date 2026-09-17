using System.Linq.Expressions;
using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetEventSignupStatsQueryHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IActivityRoleTypeRepository roleTypeRepository =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly IAssignmentStatusTypeRepository statusTypeRepository =
        Substitute.For<IAssignmentStatusTypeRepository>();
    private readonly GetEventSignupStatsQueryHandler sut;

    public GetEventSignupStatsQueryHandlerTests()
    {
        var executor = new FakeQueryExecutor();
        sut = new GetEventSignupStatsQueryHandler(
            events,
            activities,
            users,
            new ListActivityRoleTypesQueryHandler(roleTypeRepository, executor, new FakeHybridCache()),
            new ListAssignmentStatusTypesQueryHandler(
                statusTypeRepository,
                executor,
                new FakeHybridCache()
            ),
            executor
        );

        // Every scenario needs the two catalogs to resolve, even the ones that never look at
        // assignments: the response always echoes the full role and status catalogs.
        roleTypeRepository
            .Query()
            .Returns(
                new List<ActivityRoleType>
                {
                    new()
                    {
                        Id = SeedIds.ActivityRoleTypes.Leader,
                        Name = "Líder",
                        Description = "d",
                    },
                    new()
                    {
                        Id = SeedIds.ActivityRoleTypes.Volunteer,
                        Name = "Voluntario",
                        Description = "d",
                    },
                }.AsQueryable()
            );
        statusTypeRepository
            .Query()
            .Returns(
                new List<AssignmentStatusType>
                {
                    new()
                    {
                        Id = SeedIds.AssignmentStatusTypes.Requested,
                        Name = "Solicitado",
                        Description = "d",
                        Color = "#000",
                    },
                    new()
                    {
                        Id = SeedIds.AssignmentStatusTypes.Confirmed,
                        Name = "Confirmado",
                        Description = "d",
                        Color = "#0f0",
                    },
                    new()
                    {
                        Id = SeedIds.AssignmentStatusTypes.Denied,
                        Name = "Denegado",
                        Description = "d",
                        Color = "#f00",
                    },
                }.AsQueryable()
            );
    }

    private void EventExists(bool exists)
    {
        events
            .ExistsAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(exists);
    }

    private void UserType(Guid userId, Guid userTypeId)
    {
        users
            .Query()
            .Returns(
                new List<User>
                {
                    new()
                    {
                        Id = userId,
                        FirstName = "F",
                        LastName = "L",
                        UserTypeId = userTypeId,
                    },
                }.AsQueryable()
            );
    }

    private static Activity NewActivity(
        Guid eventId,
        Guid activityId,
        string title,
        DateTimeOffset startsAt
    )
    {
        return new Activity
        {
            Id = activityId,
            Title = title,
            Description = "{}",
            Location = "Sala",
            EventId = eventId,
            ActivityStartsAt = startsAt,
            ActivityEndsAt = startsAt.AddHours(1),
        };
    }

    private static ActivityUserRoleAssignment NewAssignment(
        Activity activity,
        Guid userId,
        Guid roleTypeId,
        Guid statusId,
        string firstName = "PII",
        string lastName = "Nombre",
        string? email = "pii@example.test"
    )
    {
        return new ActivityUserRoleAssignment
        {
            ActivityId = activity.Id,
            Activity = activity,
            UserId = userId,
            ActivityRoleTypeId = roleTypeId,
            AssignmentStatusId = statusId,
            User = new User
            {
                Id = userId,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                UserTypeId = SeedIds.UserTypes.Participant,
            },
        };
    }

    [Fact]
    public async Task HandleAsyncAdminNonMemberIsAuthorizedAndReturnsStats()
    {
        var eventId = Guid.NewGuid();
        EventExists(true);
        activities.Query().Returns(new List<Activity>().AsQueryable());
        activities.QueryAssignments().Returns(new List<ActivityUserRoleAssignment>().AsQueryable());

        var result = await sut.HandleAsync(
            new GetEventSignupStatsQuery(eventId, Guid.NewGuid(), IsAdmin: true),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        // Admin authorization never consults the user's type.
        users.DidNotReceiveWithAnyArgs().Query();
    }

    [Fact]
    public async Task HandleAsyncMemberUserIsAuthorizedAndReturnsStats()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        UserType(userId, SeedIds.UserTypes.Member);
        EventExists(true);
        activities.Query().Returns(new List<Activity>().AsQueryable());
        activities.QueryAssignments().Returns(new List<ActivityUserRoleAssignment>().AsQueryable());

        var result = await sut.HandleAsync(
            new GetEventSignupStatsQuery(eventId, userId, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Participant")]
    [InlineData("Sponsor")]
    public async Task HandleAsyncNonMemberUserTypeReturnsAccessDenied(string userTypeName)
    {
        var userTypeId = userTypeName == "Participant"
            ? SeedIds.UserTypes.Participant
            : SeedIds.UserTypes.Sponsor;
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        UserType(userId, userTypeId);

        var result = await sut.HandleAsync(
            new GetEventSignupStatsQuery(eventId, userId, IsAdmin: false),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ErrorCode.AccessDenied);
        // Denied before the event is even looked up.
        await events
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsEventNotFound()
    {
        var eventId = Guid.NewGuid();
        EventExists(false);

        var result = await sut.HandleAsync(
            new GetEventSignupStatsQuery(eventId, Guid.NewGuid(), IsAdmin: true),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task HandleAsyncAggregatesAssignmentsByActivityRoleStatusAndTotals()
    {
        var eventId = Guid.NewGuid();
        var earlyActivity = NewActivity(
            eventId,
            Guid.NewGuid(),
            "Taller temprano",
            new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero)
        );
        var lateActivity = NewActivity(
            eventId,
            Guid.NewGuid(),
            "Taller tardío",
            new DateTimeOffset(2026, 8, 1, 16, 0, 0, TimeSpan.Zero)
        );
        EventExists(true);
        activities.Query().Returns(new List<Activity> { lateActivity, earlyActivity }.AsQueryable());
        activities
            .QueryAssignments()
            .Returns(
                new List<ActivityUserRoleAssignment>
                {
                    NewAssignment(
                        earlyActivity,
                        Guid.NewGuid(),
                        SeedIds.ActivityRoleTypes.Leader,
                        SeedIds.AssignmentStatusTypes.Requested
                    ),
                    NewAssignment(
                        earlyActivity,
                        Guid.NewGuid(),
                        SeedIds.ActivityRoleTypes.Leader,
                        SeedIds.AssignmentStatusTypes.Requested
                    ),
                    NewAssignment(
                        earlyActivity,
                        Guid.NewGuid(),
                        SeedIds.ActivityRoleTypes.Volunteer,
                        SeedIds.AssignmentStatusTypes.Confirmed
                    ),
                    NewAssignment(
                        lateActivity,
                        Guid.NewGuid(),
                        SeedIds.ActivityRoleTypes.Leader,
                        SeedIds.AssignmentStatusTypes.Denied
                    ),
                }.AsQueryable()
            );

        var result = await sut.HandleAsync(
            new GetEventSignupStatsQuery(eventId, Guid.NewGuid(), IsAdmin: true),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.EventId.Should().Be(eventId);
        // Ordered by start time: the early activity's cells come first.
        result.Value.Activities.Select(a => a.ActivityId)
            .Should()
            .Equal(earlyActivity.Id, lateActivity.Id);

        var earlyCells = result.Value.Activities.First(a => a.ActivityId == earlyActivity.Id).Cells;
        earlyCells.Should()
            .ContainSingle(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader
                && c.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Requested
                && c.Count == 2
            );
        earlyCells.Should()
            .ContainSingle(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer
                && c.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                && c.Count == 1
            );

        var lateCells = result.Value.Activities.First(a => a.ActivityId == lateActivity.Id).Cells;
        lateCells.Should()
            .ContainSingle(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Leader
                && c.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Denied
                && c.Count == 1
            );

        result.Value.Totals.Total.Should().Be(4);
        result.Value.Totals.Requested.Should().Be(2);
        result.Value.Totals.Confirmed.Should().Be(1);
        result.Value.Totals.Denied.Should().Be(1);
        result.Value.Roles.Should().HaveCount(2);
        result.Value.Statuses.Should().HaveCount(3);
    }

    [Fact]
    public async Task HandleAsyncActivityWithoutAssignmentsAppearsWithEmptyCells()
    {
        var eventId = Guid.NewGuid();
        var emptyActivity = NewActivity(
            eventId,
            Guid.NewGuid(),
            "Sin inscripciones",
            new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero)
        );
        EventExists(true);
        activities.Query().Returns(new List<Activity> { emptyActivity }.AsQueryable());
        activities.QueryAssignments().Returns(new List<ActivityUserRoleAssignment>().AsQueryable());

        var result = await sut.HandleAsync(
            new GetEventSignupStatsQuery(eventId, Guid.NewGuid(), IsAdmin: true),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var activity = result.Value.Activities.Should().ContainSingle().Subject;
        activity.ActivityId.Should().Be(emptyActivity.Id);
        activity.Cells.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncResponseSerializedNeverContainsSeededUserPersonalData()
    {
        var eventId = Guid.NewGuid();
        var activity = NewActivity(
            eventId,
            Guid.NewGuid(),
            "Taller",
            new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero)
        );
        var seededUserId = Guid.NewGuid();
        var seededFirstName = "Carlota";
        var seededLastName = "Confidencial";
        var seededEmail = "carlota.confidencial@example.test";
        EventExists(true);
        activities.Query().Returns(new List<Activity> { activity }.AsQueryable());
        activities
            .QueryAssignments()
            .Returns(
                new List<ActivityUserRoleAssignment>
                {
                    NewAssignment(
                        activity,
                        seededUserId,
                        SeedIds.ActivityRoleTypes.Leader,
                        SeedIds.AssignmentStatusTypes.Confirmed,
                        seededFirstName,
                        seededLastName,
                        seededEmail
                    ),
                }.AsQueryable()
            );

        var result = await sut.HandleAsync(
            new GetEventSignupStatsQuery(eventId, Guid.NewGuid(), IsAdmin: true),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var json = JsonSerializer.Serialize(result.Value);
        var lowerJson = json.ToLowerInvariant();

        // This is the privacy invariant the design mandates: only aggregated counts and catalog
        // labels may leave the handler. If a future change appends user identity to the response
        // (under any casing convention), one of these assertions must fail.
        json.Should().NotContain(seededUserId.ToString());
        json.Should().NotContain(seededFirstName);
        json.Should().NotContain(seededLastName);
        json.Should().NotContain(seededEmail);
        lowerJson.Should().NotContain("firstname", "no per-user field should ever be added to this DTO");
        lowerJson.Should().NotContain("lastname", "no per-user field should ever be added to this DTO");
        lowerJson.Should().NotContain("email", "no per-user field should ever be added to this DTO");
        lowerJson.Should().NotContain("userid", "no per-user field should ever be added to this DTO");
    }
}
