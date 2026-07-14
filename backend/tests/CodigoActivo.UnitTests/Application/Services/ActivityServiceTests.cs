using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
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

    private void HasEvents(params Event[] items) => events.Query().Returns(items.AsQueryable());

    private Event EventExists()
    {
        var ev = NewEvent();
        HasEvents(ev);
        return ev;
    }

    private void EventExistsFor(Activity activity) => HasEvents(NewEvent(id: activity.EventId));

    private void HasRoleCatalog()
    {
        var catalog = new List<ActivityRoleType>
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
            new()
            {
                Id = SeedIds.ActivityRoleTypes.Participant,
                Name = "Participante",
                Description = "d",
            },
        };
        roleTypes
            .CountAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(ci =>
                catalog.Count(ci.Arg<Expression<Func<ActivityRoleType, bool>>>().Compile().Invoke)
            );
    }

    private void ActivityFound(Activity? activity)
    {
        activities
            .FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(activity);
        activities
            .FindWithRoleCapacitiesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(activity);
    }

    private static Event NewEvent(Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
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
        Guid? eventId = null,
        Guid? modalityId = null,
        string modalityName = "Presencial",
        string location = "Sala",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null
    ) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Description = "{}",
            Location = location,
            ActivityStartsAt = startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            ActivityEndsAt = endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            EventId = eventId ?? Guid.NewGuid(),
            ActivityModalityTypeId = modalityId ?? Guid.NewGuid(),
            ActivityModalityType = new ActivityModalityType { Name = modalityName },
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };

    private static CreateActivityRequest CreateRequest(
        string title = "  Taller  ",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        IReadOnlyList<ActivityRoleCapacityRequest>? roleCapacities = null
    ) =>
        new(
            title,
            "{}",
            "  Sala  ",
            Guid.NewGuid(),
            startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            Guid.NewGuid(),
            roleCapacities
        );

    private static UpdateActivityRequest UpdateRequest(
        string title = "  New  ",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        Guid? thumbnailId = null,
        IReadOnlyList<ActivityRoleCapacityRequest>? roleCapacities = null
    ) =>
        new(
            title,
            "{}",
            "  Sala  ",
            Guid.NewGuid(),
            startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            thumbnailId ?? Guid.NewGuid(),
            roleCapacities
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
    public async Task ListAsync_ModalityTypeIdFilter_ReturnsMatchingActivity()
    {
        var modalityId = Guid.NewGuid();
        HasActivities(
            NewActivity("En sala", modalityId: modalityId),
            NewActivity("En remoto", modalityName: "Online")
        );

        var result = await sut.ListAsync(
            new ActivityListQuery { ModalityTypeId = modalityId },
            TestContext.Current.CancellationToken
        );

        var item = result.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("En sala");
        item.ModalityId.Should().Be(modalityId);
    }

    [Fact]
    public async Task ListAsync_SortByModalityName_OrdersByModalityName()
    {
        HasActivities(
            NewActivity("Tercera", modalityName: "Presencial"),
            NewActivity("Primera", modalityName: "Híbrida"),
            NewActivity("Segunda", modalityName: "Online")
        );

        var result = await sut.ListAsync(
            new ActivityListQuery { Sort = "modalityName" },
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(a => a.ModalityName)
            .Should()
            .ContainInOrder("Híbrida", "Online", "Presencial");
    }

    [Fact]
    public async Task ListAsync_LocationSearch_IsAccentAndCaseInsensitive()
    {
        HasActivities(
            NewActivity("Con acento", location: "Salón Ávila"),
            NewActivity("Otra", location: "Patio")
        );

        var result = await sut.ListAsync(
            new ActivityListQuery { Location = "avila" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Con acento");
    }

    [Fact]
    public async Task ListAsync_ActivityDateRangeFilter_KeepsActivitiesOverlappingRange()
    {
        HasActivities(
            NewActivity(
                "Antes",
                startsAt: new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero)
            ),
            NewActivity(
                "Dentro",
                startsAt: new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)
            ),
            NewActivity(
                "Despues",
                startsAt: new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero)
            )
        );

        var result = await sut.ListAsync(
            new ActivityListQuery
            {
                ActivityDateFrom = new DateOnly(2026, 7, 10),
                ActivityDateTo = new DateOnly(2026, 7, 10),
            },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Dentro");
    }

    [Fact]
    public async Task ListAsync_ActivityDateFromFilter_UsesAppTimeZoneDayStart()
    {
        clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        HasActivities(
            NewActivity(
                "Madrugada",
                startsAt: new DateTimeOffset(2026, 7, 9, 22, 30, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 9, 23, 0, 0, TimeSpan.Zero)
            ),
            NewActivity(
                "Anterior",
                startsAt: new DateTimeOffset(2026, 7, 9, 20, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 7, 9, 21, 0, 0, TimeSpan.Zero)
            )
        );

        var result = await sut.ListAsync(
            new ActivityListQuery { ActivityDateFrom = new DateOnly(2026, 7, 10) },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Madrugada");
    }

    [Fact]
    public async Task ListAsync_SortByLocation_OrdersByLocation()
    {
        HasActivities(
            NewActivity("Ultima", location: "Zaguán"),
            NewActivity("Primera", location: "Aula"),
            NewActivity("Segunda", location: "Mercado")
        );

        var result = await sut.ListAsync(
            new ActivityListQuery { Sort = "location" },
            TestContext.Current.CancellationToken
        );

        result.Items.Select(a => a.Location).Should().ContainInOrder("Aula", "Mercado", "Zaguán");
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
        HasEvents();

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
        var ev = EventExists();

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
            ev.Id,
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
        var ev = EventExists();
        var when = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero);

        var result = await sut.CreateAsync(
            ev.Id,
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
        var ev = EventExists();

        var result = await sut.CreateAsync(
            ev.Id,
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
    public async Task CreateAsync_StartsBeforeEventRange_ReturnsOutsideEventRange()
    {
        var ev = EventExists();

        var result = await sut.CreateAsync(
            ev.Id,
            CreateRequest(
                startsAt: new DateTimeOffset(2026, 6, 25, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero)
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
        var ev = EventExists();
        ThumbnailExists(false);

        var result = await sut.CreateAsync(
            ev.Id,
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
        var ev = EventExists();
        ThumbnailExists(true);
        ModalityExists(false);

        var result = await sut.CreateAsync(
            ev.Id,
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
    public async Task CreateAsync_ValidRequest_PersistsTrimmedActivityAndReturnsProjection()
    {
        var eventId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        HasEvents(NewEvent(id: eventId));
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
    public async Task CreateAsync_WithRoleCapacities_PersistsDesiredCounts()
    {
        var ev = EventExists();
        ThumbnailExists(true);
        ModalityExists(true);
        HasRoleCatalog();

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
            ev.Id,
            CreateRequest(
                roleCapacities:
                [
                    new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 12),
                    new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Volunteer, 3),
                ]
            ),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var saved = stored.Single();
        saved.RoleCapacities.Should().HaveCount(2);
        saved
            .RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
            )
            .DesiredCount.Should()
            .Be(12);
        saved
            .RoleCapacities.Single(c => c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer)
            .DesiredCount.Should()
            .Be(3);
        result
            .Value.RoleCapacities.Should()
            .BeEquivalentTo([
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Participant, 12, false),
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Volunteer, 3, false),
            ]);
    }

    [Fact]
    public async Task CreateAsync_DuplicatedRoleCapacityRole_ReturnsBadRequest()
    {
        var ev = EventExists();
        ThumbnailExists(true);
        ModalityExists(true);
        HasRoleCatalog();

        var result = await sut.CreateAsync(
            ev.Id,
            CreateRequest(
                roleCapacities:
                [
                    new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 5),
                    new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 8),
                ]
            ),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleCapacityDuplicated);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_UnknownRoleCapacityRole_ReturnsRoleTypeNotFound()
    {
        var ev = EventExists();
        ThumbnailExists(true);
        ModalityExists(true);
        HasRoleCatalog();

        var result = await sut.CreateAsync(
            ev.Id,
            CreateRequest(roleCapacities: [new ActivityRoleCapacityRequest(Guid.NewGuid(), 5)]),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ActivityMissing_ReturnsNotFound()
    {
        ActivityFound(null);

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
        ActivityFound(activity);
        HasEvents();

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
        ActivityFound(activity);
        EventExistsFor(activity);

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
        ActivityFound(activity);
        EventExistsFor(activity);
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
        ActivityFound(activity);
        EventExistsFor(activity);
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
    public async Task UpdateAsync_ValidRequest_MutatesAndPersists()
    {
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var activity = NewActivity(title: "Old", id: activityId, eventId: eventId);

        var stored = new List<Activity> { activity };
        activities.Query().Returns(_ => stored.AsQueryable());
        ActivityFound(activity);
        EventExistsFor(activity);
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
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithRoleCapacities_SyncsCollection()
    {
        var activity = NewActivity();
        activity.RoleCapacities =
        [
            new ActivityRoleCapacity
            {
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                DesiredCount = 5,
            },
            new ActivityRoleCapacity
            {
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Leader,
                DesiredCount = 1,
            },
        ];
        HasActivities(activity);
        ActivityFound(activity);
        EventExistsFor(activity);
        ThumbnailExists(true);
        ModalityExists(true);
        HasRoleCatalog();

        var result = await sut.UpdateAsync(
            activity.Id,
            UpdateRequest(
                roleCapacities:
                [
                    new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Participant, 2),
                    new ActivityRoleCapacityRequest(SeedIds.ActivityRoleTypes.Volunteer, 4),
                ]
            ),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.RoleCapacities.Should().HaveCount(2);
        activity
            .RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
            )
            .DesiredCount.Should()
            .Be(2);
        activity
            .RoleCapacities.Single(c => c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer)
            .DesiredCount.Should()
            .Be(4);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NullRoleCapacities_ClearsExisting()
    {
        var activity = NewActivity();
        activity.RoleCapacities =
        [
            new ActivityRoleCapacity
            {
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                DesiredCount = 5,
            },
        ];
        HasActivities(activity);
        ActivityFound(activity);
        EventExistsFor(activity);
        ThumbnailExists(true);
        ModalityExists(true);

        var result = await sut.UpdateAsync(
            activity.Id,
            UpdateRequest(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activity.RoleCapacities.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_AssignmentsExceedDesiredCount_FlagsOnlySaturatedRole()
    {
        var activity = NewActivity();
        activity.RoleCapacities =
        [
            new ActivityRoleCapacity
            {
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                DesiredCount = 1,
            },
            new ActivityRoleCapacity
            {
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Volunteer,
                DesiredCount = 2,
            },
        ];
        activity.Assignments =
        [
            new ActivityUserRoleAssignment
            {
                UserId = Guid.NewGuid(),
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                AssignmentStatusId = SeedIds.AssignmentStatusTypes.Confirmed,
            },
            new ActivityUserRoleAssignment
            {
                UserId = Guid.NewGuid(),
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                AssignmentStatusId = SeedIds.AssignmentStatusTypes.Requested,
            },
        ];
        HasActivities(activity);

        var result = await sut.GetByIdAsync(activity.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result
            .Value.RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Participant
            )
            .Should()
            .BeEquivalentTo(
                new ActivityRoleCapacityResponse(SeedIds.ActivityRoleTypes.Participant, 1, true)
            );
        result
            .Value.RoleCapacities.Single(c =>
                c.ActivityRoleTypeId == SeedIds.ActivityRoleTypes.Volunteer
            )
            .IsHighDemand.Should()
            .BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_NonDeniedAssignmentsAtDesiredCount_RoleNotHighDemand()
    {
        var activity = NewActivity();
        activity.RoleCapacities =
        [
            new ActivityRoleCapacity
            {
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                DesiredCount = 1,
            },
        ];
        activity.Assignments =
        [
            new ActivityUserRoleAssignment
            {
                UserId = Guid.NewGuid(),
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                AssignmentStatusId = SeedIds.AssignmentStatusTypes.Confirmed,
            },
            new ActivityUserRoleAssignment
            {
                UserId = Guid.NewGuid(),
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                AssignmentStatusId = SeedIds.AssignmentStatusTypes.Denied,
            },
            new ActivityUserRoleAssignment
            {
                UserId = Guid.NewGuid(),
                ActivityId = activity.Id,
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Volunteer,
                AssignmentStatusId = SeedIds.AssignmentStatusTypes.Confirmed,
            },
        ];
        HasActivities(activity);

        var result = await sut.GetByIdAsync(activity.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleCapacities.Should().OnlyContain(c => !c.IsHighDemand);
    }

    [Fact]
    public async Task UpdateAsync_ReplacingThumbnail_CleansUpPreviousFileAfterSave()
    {
        var activity = NewActivity();
        var previousThumbnailId = activity.ThumbnailId;
        HasActivities(activity);
        ActivityFound(activity);
        EventExistsFor(activity);
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
        ActivityFound(activity);
        EventExistsFor(activity);
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
        ActivityFound(null);

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

        var result = await sut.ListAssignedAsync(
            userId,
            eventId: null,
            TestContext.Current.CancellationToken
        );

        result.Select(a => a.Title).Should().ContainInOrder("Early", "Late");
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAssignedAsync_EventIdFilter_ExcludesOtherEventAssignments()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        activities
            .QueryAssignments()
            .Returns(
                new List<ActivityUserRoleAssignment>
                {
                    Assignment(
                        userId,
                        "Mine",
                        new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero),
                        eventId
                    ),
                    Assignment(
                        userId,
                        "OtherEvent",
                        new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)
                    ),
                }.AsQueryable()
            );

        var result = await sut.ListAssignedAsync(
            userId,
            eventId,
            TestContext.Current.CancellationToken
        );

        var assigned = result.Should().ContainSingle().Subject;
        assigned.Title.Should().Be("Mine");
        assigned.EventId.Should().Be(eventId);
    }

    private static ActivityUserRoleAssignment Assignment(
        Guid userId,
        string title,
        DateTimeOffset startsAt,
        Guid? eventId = null
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
                EventId = eventId ?? Guid.NewGuid(),
            },
            ActivityRoleTypeId = Guid.NewGuid(),
            ActivityRoleType = new ActivityRoleType { Name = "Líder" },
            AssignmentStatusId = Guid.NewGuid(),
            AssignmentStatus = new AssignmentStatusType { Name = "Solicitado", Color = "#000" },
        };
}
