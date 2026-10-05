using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class ActivityTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private static readonly DateOnly EventStart = new(2026, 7, 1);
    private static readonly DateOnly EventEnd = new(2026, 7, 31);
    private static readonly DateTimeOffset StartsAt = new(2026, 7, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone(
        "Test",
        TimeSpan.FromHours(2),
        "Test",
        "Test"
    );

    public static TheoryData<DateTimeOffset?, DateTimeOffset?> MissingTimes =>
        new() { { null, StartsAt.AddHours(2) }, { StartsAt, null } };

    public static TheoryData<DateTimeOffset, DateTimeOffset, int> TimesOutsideLocalEventDays =>
        new()
        {
            {
                new DateTimeOffset(2026, 7, 1, 3, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 1, 4, 0, 0, TimeSpan.Zero),
                -5
            },
            {
                new DateTimeOffset(2026, 7, 31, 21, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 31, 22, 30, 0, TimeSpan.Zero),
                2
            },
        };

    private static ActivitySchedule Schedule(DateTimeOffset? startsAt = null)
    {
        var start = startsAt ?? StartsAt;
        return ActivitySchedule
            .Create(start, start.AddHours(2), EventStart, EventEnd, TimeZoneInfo.Utc)
            .Value;
    }

    private static RoleCapacityPlan Plan(params RoleCapacity[] capacities)
    {
        return RoleCapacityPlan.Create(capacities).Value;
    }

    private static Activity NewActivity(RoleCapacityPlan? capacities = null)
    {
        return Activity.Create(
            Guid.NewGuid(),
            new ActivityDetails("Taller", "{}", "Sala", Guid.NewGuid(), Guid.NewGuid()),
            Schedule(),
            capacities ?? RoleCapacityPlan.None,
            Guid.NewGuid(),
            Now
        );
    }

    [Theory]
    [MemberData(nameof(MissingTimes))]
    public void ScheduleCreateMissingTimeReturnsScheduleRequired(
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt
    )
    {
        var schedule = ActivitySchedule.Create(
            startsAt,
            endsAt,
            EventStart,
            EventEnd,
            TimeZoneInfo.Utc
        );

        schedule.ShouldFail(ErrorKind.Validation, ErrorCode.ActivityScheduleRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void ScheduleCreateEndNotAfterStartReturnsInvalidRange(int endsMinutesAfterStart)
    {
        var schedule = ActivitySchedule.Create(
            StartsAt,
            StartsAt.AddMinutes(endsMinutesAfterStart),
            EventStart,
            EventEnd,
            TimeZoneInfo.Utc
        );

        schedule.ShouldFail(ErrorKind.Validation, ErrorCode.ActivityScheduleInvalidRange);
    }

    [Theory]
    [MemberData(nameof(TimesOutsideLocalEventDays))]
    public void ScheduleCreateOutsideEventDaysInLocalZoneReturnsOutsideEventRange(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int zoneOffsetHours
    )
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone(
            "Test",
            TimeSpan.FromHours(zoneOffsetHours),
            "Test",
            "Test"
        );

        var inUtc = ActivitySchedule.Create(
            startsAt,
            endsAt,
            EventStart,
            EventEnd,
            TimeZoneInfo.Utc
        );
        var inZone = ActivitySchedule.Create(startsAt, endsAt, EventStart, EventEnd, zone);

        inUtc.IsSuccess.Should().BeTrue();
        inZone.ShouldFail(ErrorKind.Validation, ErrorCode.ActivityScheduleOutsideEventRange);
    }

    [Fact]
    public void ScheduleCreateInsideEventDaysInLocalZoneReturnsUtcTimes()
    {
        var offset = TimeSpan.FromHours(2);
        var startsAt = new DateTimeOffset(2026, 7, 1, 0, 30, 0, offset);
        var endsAt = new DateTimeOffset(2026, 7, 1, 2, 0, 0, offset);

        var schedule = ActivitySchedule.Create(startsAt, endsAt, EventStart, EventEnd, PlusTwo);

        ActivitySchedule
            .Create(startsAt, endsAt, EventStart, EventEnd, TimeZoneInfo.Utc)
            .ShouldFail(ErrorKind.Validation, ErrorCode.ActivityScheduleOutsideEventRange);
        schedule
            .Value.StartsAt.Should()
            .BeExactly(new DateTimeOffset(2026, 6, 30, 22, 30, 0, TimeSpan.Zero));
        schedule
            .Value.EndsAt.Should()
            .BeExactly(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void CapacityPlanCreateRoleListedTwiceReturnsRoleCapacityDuplicated()
    {
        var plan = RoleCapacityPlan.Create([
            new RoleCapacity(SeedIds.ActivityRoleTypes.Participant, 5),
            new RoleCapacity(SeedIds.ActivityRoleTypes.Participant, 8),
        ]);

        plan.ShouldFail(ErrorKind.Validation, ErrorCode.ActivityRoleCapacityDuplicated);
    }

    [Fact]
    public void CapacityPlanCreateNoCapacitiesReturnsNone()
    {
        RoleCapacityPlan.Create(null).Value.Should().BeSameAs(RoleCapacityPlan.None);
        RoleCapacityPlan.Create([]).Value.Should().BeSameAs(RoleCapacityPlan.None);
        RoleCapacityPlan.None.Items.Should().BeEmpty();
    }

    [Fact]
    public void CapacityPlanCreateDistinctRolesKeepsThem()
    {
        var participants = new RoleCapacity(SeedIds.ActivityRoleTypes.Participant, 12);
        var volunteers = new RoleCapacity(SeedIds.ActivityRoleTypes.Volunteer, 3);

        var plan = RoleCapacityPlan.Create([participants, volunteers]);

        plan.Value.Items.Should().Equal(participants, volunteers);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void HasStartedByComparesWithStartTime(int minutesAfterStart, bool started)
    {
        var activity = Activity.Create(
            Guid.NewGuid(),
            new ActivityDetails("Taller", "{}", "Sala", Guid.NewGuid(), Guid.NewGuid()),
            Schedule(),
            Plan(),
            Guid.NewGuid(),
            Now
        );

        activity.HasStartedBy(StartsAt.AddMinutes(minutesAfterStart)).Should().Be(started);
    }

    [Fact]
    public void CreateDetailsWithSpacesStoresTrimmedDetailsCapacitiesAndAuthor()
    {
        var eventId = Guid.NewGuid();
        var modalityId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var schedule = Schedule();

        var activity = Activity.Create(
            eventId,
            new ActivityDetails("  Taller ", "{\"a\":1}", " Sala A  ", modalityId, thumbnailId),
            schedule,
            Plan(new RoleCapacity(SeedIds.ActivityRoleTypes.Participant, 12)),
            authorId,
            Now
        );

        activity.Id.Should().NotBeEmpty();
        activity.EventId.Should().Be(eventId);
        activity.Title.Should().Be("Taller");
        activity.Description.Should().Be("{\"a\":1}");
        activity.Location.Should().Be("Sala A");
        activity.ActivityModalityTypeId.Should().Be(modalityId);
        activity.ThumbnailId.Should().Be(thumbnailId);
        activity.ActivityStartsAt.Should().Be(schedule.StartsAt);
        activity.ActivityEndsAt.Should().Be(schedule.EndsAt);
        var capacity = activity.RoleCapacities.Should().ContainSingle().Which;
        capacity.ActivityId.Should().Be(activity.Id);
        capacity.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Participant);
        capacity.DesiredCount.Should().Be(12);
        activity.Assignments.Should().BeEmpty();
        activity.CreatedBy.Should().Be(authorId);
        activity.CreatedAt.Should().Be(Now);
        activity.UpdatedBy.Should().BeNull();
        activity.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void UpdateCapacitiesResizesKeptRolesAddsNewAndDropsTheRest()
    {
        var activity = NewActivity(
            Plan(
                new RoleCapacity(SeedIds.ActivityRoleTypes.Participant, 5),
                new RoleCapacity(SeedIds.ActivityRoleTypes.Leader, 1)
            )
        );
        var participants = activity.RoleCapacities.Single(capacity =>
            capacity.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
        );

        activity.Update(
            new ActivityDetails("Taller", "{}", "Sala", Guid.NewGuid(), activity.ThumbnailId),
            Schedule(),
            Plan(
                new RoleCapacity(SeedIds.ActivityRoleTypes.Participant, 2),
                new RoleCapacity(SeedIds.ActivityRoleTypes.Volunteer, 4)
            ),
            Guid.NewGuid(),
            Now
        );

        activity
            .RoleCapacities.Select(capacity => (capacity.ActivityRoleTypeId, capacity.DesiredCount))
            .Should()
            .BeEquivalentTo([
                (SeedIds.ActivityRoleTypes.Participant, 2),
                (SeedIds.ActivityRoleTypes.Volunteer, 4),
            ]);
        activity.RoleCapacities.Should().Contain(participants);
        activity
            .RoleCapacities.Should()
            .OnlyContain(capacity => capacity.ActivityId == activity.Id);
    }

    [Fact]
    public void UpdateNewDetailsReplacesThemKeepsSignupsAndRecordsEditor()
    {
        var activity = NewActivity(Plan(new RoleCapacity(SeedIds.ActivityRoleTypes.Leader, 1)));
        var userId = Guid.NewGuid();
        activity.RequestAssignment(userId, SeedIds.ActivityRoleTypes.Participant, Now);
        var modalityId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        var schedule = Schedule(StartsAt.AddDays(1));

        activity.Update(
            new ActivityDetails(" Charla ", "{\"b\":2}", " Aula ", modalityId, thumbnailId),
            schedule,
            RoleCapacityPlan.None,
            editorId,
            Now.AddDays(1)
        );

        activity.Title.Should().Be("Charla");
        activity.Description.Should().Be("{\"b\":2}");
        activity.Location.Should().Be("Aula");
        activity.ActivityModalityTypeId.Should().Be(modalityId);
        activity.ThumbnailId.Should().Be(thumbnailId);
        activity.ActivityStartsAt.Should().Be(schedule.StartsAt);
        activity.ActivityEndsAt.Should().Be(schedule.EndsAt);
        activity.RoleCapacities.Should().BeEmpty();
        activity.AssignmentOf(userId).Should().NotBeNull();
        activity.CreatedAt.Should().Be(Now);
        activity.UpdatedBy.Should().Be(editorId);
        activity.UpdatedAt.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void RequestAssignmentPersonNotSignedUpAddsRequestedSignup()
    {
        var activity = NewActivity();
        var userId = Guid.NewGuid();

        var result = activity.RequestAssignment(userId, SeedIds.ActivityRoleTypes.Volunteer, Now);

        result.IsSuccess.Should().BeTrue();
        var assignment = activity.Assignments.Should().ContainSingle().Which;
        assignment.UserId.Should().Be(userId);
        assignment.ActivityId.Should().Be(activity.Id);
        assignment.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Volunteer);
        assignment.AssignmentStatusId.Should().Be(SeedIds.AssignmentStatusTypes.Requested);
        assignment.CreatedAt.Should().Be(Now);
        activity.AssignmentOf(userId).Should().BeSameAs(assignment);
    }

    [Fact]
    public void RequestAssignmentPersonAlreadySignedUpReturnsConflict()
    {
        var activity = NewActivity();
        var userId = Guid.NewGuid();
        activity.RequestAssignment(userId, SeedIds.ActivityRoleTypes.Participant, Now);

        var result = activity.RequestAssignment(userId, SeedIds.ActivityRoleTypes.Volunteer, Now);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.ActivityAssignmentAlreadyExists);
        activity
            .Assignments.Should()
            .ContainSingle()
            .Which.ActivityRoleTypeId.Should()
            .Be(SeedIds.ActivityRoleTypes.Participant);
    }

    [Fact]
    public void AssignmentOfPersonNotSignedUpReturnsNull()
    {
        var activity = NewActivity();
        activity.RequestAssignment(Guid.NewGuid(), SeedIds.ActivityRoleTypes.Participant, Now);

        activity.AssignmentOf(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void UnassignSignedUpPersonRemovesTheSignup()
    {
        var activity = NewActivity();
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        activity.RequestAssignment(userId, SeedIds.ActivityRoleTypes.Participant, Now);
        activity.RequestAssignment(otherId, SeedIds.ActivityRoleTypes.Participant, Now);

        var result = activity.Unassign(userId);

        result.IsSuccess.Should().BeTrue();
        activity.AssignmentOf(userId).Should().BeNull();
        activity.Assignments.Should().ContainSingle().Which.UserId.Should().Be(otherId);
    }

    [Fact]
    public void UnassignPersonNotSignedUpReturnsNotFound()
    {
        var activity = NewActivity();

        var result = activity.Unassign(Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.ActivityAssignmentNotFound);
    }

    [Fact]
    public void ChangeAssignmentRoleNewRoleKeepsStatusAndDate()
    {
        var activity = NewActivity();
        var userId = Guid.NewGuid();
        activity.RequestAssignment(userId, SeedIds.ActivityRoleTypes.Participant, Now);
        activity.ChangeAssignmentStatus(userId, SeedIds.AssignmentStatusTypes.Confirmed);

        var changed = activity.ChangeAssignmentRole(userId, SeedIds.ActivityRoleTypes.Leader);

        changed.Should().BeTrue();
        var assignment = activity.Assignments.Should().ContainSingle().Which;
        assignment.UserId.Should().Be(userId);
        assignment.ActivityId.Should().Be(activity.Id);
        assignment.ActivityRoleTypeId.Should().Be(SeedIds.ActivityRoleTypes.Leader);
        assignment.AssignmentStatusId.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        assignment.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void ChangeAssignmentRoleSameRoleReturnsFalseAndKeepsTheSignup()
    {
        var activity = NewActivity();
        var userId = Guid.NewGuid();
        activity.RequestAssignment(userId, SeedIds.ActivityRoleTypes.Volunteer, Now);
        var assignment = activity.AssignmentOf(userId);

        var changed = activity.ChangeAssignmentRole(userId, SeedIds.ActivityRoleTypes.Volunteer);

        changed.Should().BeFalse();
        activity.Assignments.Should().ContainSingle().Which.Should().BeSameAs(assignment);
    }

    [Fact]
    public void ChangeAssignmentRolePersonNotSignedUpThrows()
    {
        var activity = NewActivity();

        var act = () =>
            activity.ChangeAssignmentRole(Guid.NewGuid(), SeedIds.ActivityRoleTypes.Leader);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ChangeAssignmentStatusNewStatusReturnsThePreviousOne()
    {
        var activity = NewActivity();
        var userId = Guid.NewGuid();
        activity.RequestAssignment(userId, SeedIds.ActivityRoleTypes.Participant, Now);

        var fromRequested = activity.ChangeAssignmentStatus(
            userId,
            SeedIds.AssignmentStatusTypes.Confirmed
        );
        var fromConfirmed = activity.ChangeAssignmentStatus(
            userId,
            SeedIds.AssignmentStatusTypes.Denied
        );

        fromRequested.Should().Be(SeedIds.AssignmentStatusTypes.Requested);
        fromConfirmed.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        activity
            .AssignmentOf(userId)!
            .AssignmentStatusId.Should()
            .Be(SeedIds.AssignmentStatusTypes.Denied);
    }

    [Fact]
    public void ChangeAssignmentStatusPersonNotSignedUpThrows()
    {
        var activity = NewActivity();

        var act = () =>
            activity.ChangeAssignmentStatus(
                Guid.NewGuid(),
                SeedIds.AssignmentStatusTypes.Confirmed
            );

        act.Should().Throw<InvalidOperationException>();
    }
}
