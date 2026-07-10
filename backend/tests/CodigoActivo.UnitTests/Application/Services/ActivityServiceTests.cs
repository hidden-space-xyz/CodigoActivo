using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class ActivityServiceTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IFileService fileService = Substitute.For<IFileService>();
    private readonly IAssignmentStatusTypeRepository statuses =
        Substitute.For<IAssignmentStatusTypeRepository>();
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly IActivityModalityTypeRepository modalityTypes =
        Substitute.For<IActivityModalityTypeRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ActivityService sut;

    public ActivityServiceTests()
    {
        sut = new ActivityService(
            activities,
            events,
            files,
            fileService,
            statuses,
            roleTypes,
            modalityTypes,
            users,
            new FakeQueryExecutor(),
            clock,
            uow
        );
    }

    private void HasActivities(params Activity[] items) =>
        activities.Query().Returns(items.AsQueryable());

    private void ModalityExists(bool exists) =>
        modalityTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<ActivityModalityType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(exists);

    private void ThumbnailExists(bool exists) =>
        files
            .ExistsAsync(
                Arg.Any<Expression<Func<FileEntity, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(exists);

    private void EventFound(Event? ev) =>
        events
            .FindAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ev);

    private static Event NewEvent() =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Feria",
            Subtitle = "s",
            EventStartsAt = new DateOnly(2026, 7, 1),
            EventEndsAt = new DateOnly(2026, 7, 31),
            SignupStartsAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt = new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
        };

    private static Activity NewActivity(
        string title = "Taller",
        Guid? id = null,
        Guid? eventId = null
    ) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Description = "{}",
            Location = "Sala",
            ActivityStartsAt = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            ActivityEndsAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            EventId = eventId ?? Guid.NewGuid(),
            ActivityModalityTypeId = Guid.NewGuid(),
            ActivityModalityType = new ActivityModalityType { Name = "Presencial" },
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
            AllowedRoleTypes = [],
        };

    private static CreateActivityRequest CreateRequest(
        string title = "  Taller  ",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        IReadOnlyList<ActivityAllowedRoleRequest>? roles = null
    ) =>
        new(
            title,
            "{}",
            "  Sala  ",
            Guid.NewGuid(),
            startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            roles
        );

    private static UpdateActivityRequest UpdateRequest(
        string title = "  New  ",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        IReadOnlyList<ActivityAllowedRoleRequest>? roles = null,
        Guid? thumbnailId = null
    ) =>
        new(
            title,
            "{}",
            "  Sala  ",
            Guid.NewGuid(),
            startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            thumbnailId ?? Guid.NewGuid(),
            roles
        );

    [Fact]
    public async Task ListAsync_EventIdFilter_ReturnsMatchingActivity()
    {
        var eventId = Guid.NewGuid();
        HasActivities(NewActivity("Mine", eventId: eventId), NewActivity("Other"));

        var result = await sut.ListAsync(
            new ActivityListQuery { EventId = eventId },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Mine");
    }

    [Fact]
    public async Task ListAsync_TitleSearch_IsAccentAndCaseInsensitive()
    {
        HasActivities(NewActivity("Reunión Ávila"), NewActivity("Banco"));

        var result = await sut.ListAsync(
            new ActivityListQuery { Title = "avila" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión Ávila");
    }

    [Fact]
    public async Task ListAsync_ExplicitDescendingSort_OrdersDescending()
    {
        HasActivities(NewActivity("Alpha"), NewActivity("Zeta"), NewActivity("Mint"));

        var result = await sut.ListAsync(
            new ActivityListQuery { Sort = "-title" },
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Title).Should().ContainInOrder("Zeta", "Mint", "Alpha");
    }

    [Fact]
    public async Task GetByIdAsync_ActivityExists_ReturnsActivity()
    {
        var activity = NewActivity();
        HasActivities(activity);

        var result = await sut.GetByIdAsync(activity.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(activity.Id);
        result.Value.ModalityName.Should().Be("Presencial");
    }

    [Fact]
    public async Task GetByIdAsync_ActivityMissing_ReturnsNotFound()
    {
        HasActivities();

        var result = await sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task CreateAsync_EventMissing_ReturnsNotFound()
    {
        EventFound(null);

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_StartMissing_ReturnsScheduleRequired()
    {
        EventFound(NewEvent());

        var request = new CreateActivityRequest(
            "  Taller  ",
            "{}",
            "  Sala  ",
            Guid.NewGuid(),
            null,
            new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            null
        );

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_EndNotAfterStart_ReturnsInvalidRange()
    {
        EventFound(NewEvent());
        var when = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero);

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(startsAt: when, endsAt: when),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleInvalidRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_DatesExceedEventRange_ReturnsOutsideEventRange()
    {
        EventFound(NewEvent());

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(
                startsAt: new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero)
            ),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleOutsideEventRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ThumbnailMissing_ReturnsThumbnailNotFound()
    {
        EventFound(NewEvent());
        ThumbnailExists(false);

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ModalityMissing_ReturnsModalityTypeNotFound()
    {
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(false);

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityModalityTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_AllowedRoleUnknown_ReturnsRoleTypeNotFound()
    {
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);
        roleTypes
            .CountAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1);
        var roles = new List<ActivityAllowedRoleRequest>
        {
            new(Guid.NewGuid()),
            new(Guid.NewGuid()),
        };

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(roles: roles),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsTrimmedActivityAndReturnsProjection()
    {
        var eventId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);

        var stored = new List<Activity>();
        activities.Query().Returns(_ => stored.AsQueryable());
        activities
            .When(a => a.AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var a = ci.Arg<Activity>();
                a.ActivityModalityType = new ActivityModalityType { Name = "Presencial" };
                stored.Add(a);
            });

        var result = await sut.CreateAsync(
            eventId,
            CreateRequest(),
            caller,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Taller");
        result.Value.Location.Should().Be("Sala");
        result.Value.EventId.Should().Be(eventId);
        await activities
            .Received(1)
            .AddAsync(
                Arg.Is<Activity>(a =>
                    a.Title == "Taller"
                    && a.Location == "Sala"
                    && a.EventId == eventId
                    && a.CreatedBy == caller
                    && a.CreatedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_DuplicateRoleIds_AddsDistinctAllowedRoles()
    {
        var duplicate = Guid.NewGuid();
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);
        roleTypes
            .CountAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1);

        var stored = new List<Activity>();
        activities.Query().Returns(_ => stored.AsQueryable());
        Activity? captured = null;
        activities
            .When(a => a.AddAsync(Arg.Any<Activity>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                captured = ci.Arg<Activity>();
                captured.ActivityModalityType = new ActivityModalityType { Name = "Presencial" };
                foreach (var allowed in captured.AllowedRoleTypes)
                    allowed.ActivityRoleType = new ActivityRoleType { Name = "Líder" };
                stored.Add(captured);
            });

        var roles = new List<ActivityAllowedRoleRequest> { new(duplicate), new(duplicate) };
        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(roles: roles),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        captured!
            .AllowedRoleTypes.Should()
            .ContainSingle()
            .Which.ActivityRoleTypeId.Should()
            .Be(duplicate);
    }

    [Fact]
    public async Task UpdateAsync_ActivityMissing_ReturnsNotFound()
    {
        activities
            .GetForEditAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            UpdateRequest(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ParentEventMissing_ReturnsEventNotFound()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(null);

        var result = await sut.UpdateAsync(
            activity.Id,
            UpdateRequest(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_StartMissing_ReturnsScheduleRequired()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());

        var request = new UpdateActivityRequest(
            "  New  ",
            "{}",
            "  Sala  ",
            Guid.NewGuid(),
            null,
            new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            null
        );

        var result = await sut.UpdateAsync(
            activity.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailMissing_ReturnsThumbnailNotFound()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(false);

        var result = await sut.UpdateAsync(
            activity.Id,
            UpdateRequest(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.ActivityThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ModalityMissing_ReturnsModalityTypeNotFound()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(false);

        var result = await sut.UpdateAsync(
            activity.Id,
            UpdateRequest(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.ActivityModalityTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_AllowedRoleUnknown_ReturnsRoleTypeNotFound()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);
        roleTypes
            .CountAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(0);
        var roles = new List<ActivityAllowedRoleRequest> { new(Guid.NewGuid()) };

        var result = await sut.UpdateAsync(
            activity.Id,
            UpdateRequest(roles: roles),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_MutatesClearsRolesAndPersists()
    {
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var activity = NewActivity(title: "Old", id: activityId, eventId: eventId);
        activity.AllowedRoleTypes.Add(
            new ActivityAllowedRoleType { ActivityRoleTypeId = Guid.NewGuid() }
        );

        var stored = new List<Activity> { activity };
        activities.Query().Returns(_ => stored.AsQueryable());
        activities.GetForEditAsync(activityId, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);

        var result = await sut.UpdateAsync(
            activityId,
            UpdateRequest(title: "  New  "),
            caller,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("New");
        activity.Title.Should().Be("New");
        activity.UpdatedBy.Should().Be(caller);
        activity.UpdatedAt.Should().Be(clock.UtcNow);
        activity.AllowedRoleTypes.Should().BeEmpty();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ReplacingThumbnail_CleansUpPreviousFileAfterSave()
    {
        var activity = NewActivity();
        var previousThumbnailId = activity.ThumbnailId;
        HasActivities(activity);
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);

        var result = await sut.UpdateAsync(
            activity.Id,
            UpdateRequest(thumbnailId: Guid.NewGuid()),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(previousThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_KeepingSameThumbnail_DoesNotCleanUp()
    {
        var activity = NewActivity();
        HasActivities(activity);
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);

        var result = await sut.UpdateAsync(
            activity.Id,
            UpdateRequest(thumbnailId: activity.ThumbnailId),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await fileService
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAsync_ActivityMissing_ReturnsNotFound()
    {
        activities
            .FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var result = await sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await fileService
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ListRoleTypesAsync_MultipleRoleTypes_OrdersByNameAndProjects()
    {
        roleTypes
            .Query()
            .Returns(
                new List<ActivityRoleType>
                {
                    new() { Name = "Zeta", Description = "z" },
                    new() { Name = "Alpha", Description = "a" },
                }.AsQueryable()
            );

        var result = await sut.ListRoleTypesAsync(TestContext.Current.CancellationToken);

        result.Select(r => r.Name).Should().ContainInOrder("Alpha", "Zeta");
        result.Should().AllBeOfType<ActivityRoleTypeResponse>();
    }

    [Fact]
    public async Task ListAssignmentStatusTypesAsync_MultipleStatusTypes_OrdersByNameAndProjects()
    {
        statuses
            .Query()
            .Returns(
                new List<AssignmentStatusType>
                {
                    new()
                    {
                        Name = "Confirmado",
                        Description = "c",
                        Color = "#0f0",
                    },
                    new()
                    {
                        Name = "Aprobado",
                        Description = "a",
                        Color = "#00f",
                    },
                }.AsQueryable()
            );

        var result = await sut.ListAssignmentStatusTypesAsync(
            TestContext.Current.CancellationToken
        );

        result.Select(s => s.Name).Should().ContainInOrder("Aprobado", "Confirmado");
        result.Should().AllBeOfType<AssignmentStatusTypeResponse>();
    }

    [Fact]
    public async Task ListModalityTypesAsync_MultipleModalityTypes_OrdersByNameAndProjects()
    {
        modalityTypes
            .Query()
            .Returns(
                new List<ActivityModalityType>
                {
                    new() { Name = "Presencial" },
                    new() { Name = "Online" },
                }.AsQueryable()
            );

        var result = await sut.ListModalityTypesAsync(TestContext.Current.CancellationToken);

        result.Select(m => m.Name).Should().ContainInOrder("Online", "Presencial");
        result.Should().AllBeOfType<ActivityModalityTypeResponse>();
    }

    [Fact]
    public async Task ListAssignedAsync_MultipleUsersAssigned_FiltersByUserAndOrdersByStart()
    {
        var userId = Guid.NewGuid();
        activities
            .QueryAssignments()
            .Returns(
                new List<ActivityUserRoleAssignment>
                {
                    Assignment(
                        userId,
                        "Late",
                        new DateTimeOffset(2026, 7, 10, 14, 0, 0, TimeSpan.Zero)
                    ),
                    Assignment(
                        userId,
                        "Early",
                        new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero)
                    ),
                    Assignment(
                        Guid.NewGuid(),
                        "Other",
                        new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)
                    ),
                }.AsQueryable()
            );

        var result = await sut.ListAssignedAsync(userId, TestContext.Current.CancellationToken);

        result.Select(a => a.Title).Should().ContainInOrder("Early", "Late");
        result.Should().HaveCount(2);
    }

    private static ActivityUserRoleAssignment Assignment(
        Guid userId,
        string title,
        DateTimeOffset startsAt
    ) =>
        new()
        {
            UserId = userId,
            ActivityId = Guid.NewGuid(),
            Activity = new Activity
            {
                Title = title,
                Description = "{}",
                ActivityStartsAt = startsAt,
                ActivityEndsAt = startsAt.AddHours(1),
                EventId = Guid.NewGuid(),
            },
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = new ActivityRoleType { Name = "Líder" },
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = new AssignmentStatusType { Name = "Solicitado", Color = "#000" },
        };

    [Fact]
    public async Task CreateActivityRoleTypeAsync_NameExists_ReturnsConflict()
    {
        roleTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var result = await sut.CreateActivityRoleTypeAsync(
            new CreateActivityRoleTypeRequest("Líder", null),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNameAlreadyExists);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateActivityRoleTypeAsync_ValidRequest_TrimsAndPersists()
    {
        roleTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await sut.CreateActivityRoleTypeAsync(
            new CreateActivityRoleTypeRequest("  Líder  ", "  Guía  "),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Líder");
        result.Value.Description.Should().Be("Guía");
        await roleTypes
            .Received(1)
            .AddAsync(
                Arg.Is<ActivityRoleType>(r => r.Name == "Líder" && r.Description == "Guía"),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateActivityRoleTypeAsync_NullDescription_DefaultsToEmpty()
    {
        roleTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await sut.CreateActivityRoleTypeAsync(
            new CreateActivityRoleTypeRequest("Ayudante", null),
            TestContext.Current.CancellationToken
        );

        result.Value.Description.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateActivityRoleTypeAsync_RoleTypeMissing_ReturnsNotFound()
    {
        roleTypes
            .FindAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((ActivityRoleType?)null);

        var result = await sut.UpdateActivityRoleTypeAsync(
            Guid.NewGuid(),
            new UpdateActivityRoleTypeRequest("Líder", null),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateActivityRoleTypeAsync_NameTakenByOther_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        roleTypes
            .FindAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ActivityRoleType
                {
                    Id = id,
                    Name = "Old",
                    Description = "d",
                }
            );
        roleTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var result = await sut.UpdateActivityRoleTypeAsync(
            id,
            new UpdateActivityRoleTypeRequest("Taken", null),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNameAlreadyExists);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateActivityRoleTypeAsync_ValidRequest_MutatesAndPersists()
    {
        var id = Guid.NewGuid();
        var roleType = new ActivityRoleType
        {
            Id = id,
            Name = "Old",
            Description = "old",
        };
        roleTypes
            .FindAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(roleType);
        roleTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await sut.UpdateActivityRoleTypeAsync(
            id,
            new UpdateActivityRoleTypeRequest("  New  ", "  Desc  "),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        roleType.Name.Should().Be("New");
        roleType.Description.Should().Be("Desc");
        result.Value.Name.Should().Be("New");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteActivityRoleTypeAsync_NothingRemoved_ReturnsNotFound()
    {
        roleTypes
            .RemoveAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(0);

        var result = await sut.DeleteActivityRoleTypeAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
