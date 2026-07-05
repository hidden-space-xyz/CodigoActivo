using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

/// <summary>
/// CRUD + catalog coverage for <see cref="ActivityService"/>. The read path runs against the real
/// <see cref="FakeQueryExecutor"/> over <c>list.AsQueryable()</c>, exercising the real projection,
/// <c>SortMap</c> and <c>TextSearch</c> expressions. Assignment/household logic lives in
/// <c>ActivityServiceAssignmentTests</c>.
/// </summary>
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
            .ExistsAsync(Arg.Any<Expression<Func<ActivityModalityType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(exists);

    private void ThumbnailExists(bool exists) =>
        files
            .ExistsAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
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
            AllowedRoleTypes = new List<ActivityAllowedRoleType>(),
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

    // ---- ListAsync ---------------------------------------------------------

    [Fact]
    public async Task ListAsync_projects_and_pages()
    {
        HasActivities(NewActivity("Alpha"), NewActivity("Beta"));

        var result = await sut.ListAsync(new ActivityListQuery { Page = 1, PageSize = 10 });

        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllBeOfType<ActivityResponse>();
    }

    [Fact]
    public async Task ListAsync_filters_by_event_id()
    {
        var eventId = Guid.NewGuid();
        HasActivities(NewActivity("Mine", eventId: eventId), NewActivity("Other"));

        var result = await sut.ListAsync(new ActivityListQuery { EventId = eventId });

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Mine");
    }

    [Fact]
    public async Task ListAsync_title_search_is_accent_and_case_insensitive()
    {
        HasActivities(NewActivity("Reunión Ávila"), NewActivity("Banco"));

        var result = await sut.ListAsync(new ActivityListQuery { Title = "avila" });

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Reunión Ávila");
    }

    [Fact]
    public async Task ListAsync_honours_explicit_descending_sort()
    {
        HasActivities(NewActivity("Alpha"), NewActivity("Zeta"), NewActivity("Mint"));

        var result = await sut.ListAsync(new ActivityListQuery { Sort = "-title" });

        result.Items.Select(a => a.Title).Should().ContainInOrder("Zeta", "Mint", "Alpha");
    }

    // ---- GetByIdAsync ------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_returns_activity_when_found()
    {
        var activity = NewActivity();
        HasActivities(activity);

        var result = await sut.GetByIdAsync(activity.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(activity.Id);
        result.Value.ModalityName.Should().Be("Presencial");
    }

    [Fact]
    public async Task GetByIdAsync_returns_not_found_when_missing()
    {
        HasActivities();

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    // ---- CreateAsync guards ------------------------------------------------

    [Fact]
    public async Task CreateAsync_returns_not_found_when_event_missing()
    {
        EventFound(null);

        var result = await sut.CreateAsync(Guid.NewGuid(), CreateRequest(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_returns_schedule_required_when_start_missing()
    {
        EventFound(NewEvent());

        // Built inline (CreateRequest's `?? default` coalescing would defeat the null injection).
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

        var result = await sut.CreateAsync(Guid.NewGuid(), request, Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleRequired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_returns_invalid_range_when_end_not_after_start()
    {
        EventFound(NewEvent());
        var when = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero);

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(startsAt: when, endsAt: when),
            Guid.NewGuid()
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleInvalidRange);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_returns_outside_event_range_when_dates_exceed_event()
    {
        EventFound(NewEvent());

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(
                startsAt: new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                endsAt: new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero)
            ),
            Guid.NewGuid()
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleOutsideEventRange);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_returns_thumbnail_not_found_when_missing()
    {
        EventFound(NewEvent());
        ThumbnailExists(false);

        var result = await sut.CreateAsync(Guid.NewGuid(), CreateRequest(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_returns_modality_not_found_when_missing()
    {
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(false);

        var result = await sut.CreateAsync(Guid.NewGuid(), CreateRequest(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityModalityTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_returns_role_type_not_found_when_allowed_role_unknown()
    {
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);
        roleTypes
            .CountAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        var roles = new List<ActivityAllowedRoleRequest>
        {
            new(Guid.NewGuid()),
            new(Guid.NewGuid()),
        };

        var result = await sut.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(roles: roles),
            Guid.NewGuid()
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_persists_trimmed_activity_and_returns_projection()
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

        var result = await sut.CreateAsync(eventId, CreateRequest(), caller);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Taller");
        result.Value.Location.Should().Be("Sala");
        result.Value.EventId.Should().Be(eventId);
        await activities.Received(1).AddAsync(
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
    public async Task CreateAsync_adds_distinct_allowed_roles()
    {
        var duplicate = Guid.NewGuid();
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);
        roleTypes
            .CountAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
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
        var result = await sut.CreateAsync(Guid.NewGuid(), CreateRequest(roles: roles), Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        captured!.AllowedRoleTypes.Should().ContainSingle().Which.ActivityRoleTypeId.Should().Be(duplicate);
    }

    // ---- UpdateAsync -------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_returns_not_found_when_activity_missing()
    {
        activities.GetForEditAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var result = await sut.UpdateAsync(Guid.NewGuid(), UpdateRequest(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_returns_event_not_found_when_parent_event_missing()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(null);

        var result = await sut.UpdateAsync(activity.Id, UpdateRequest(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_returns_schedule_required_when_start_missing()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());

        // Built inline (UpdateRequest's `?? default` coalescing would defeat the null injection).
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

        var result = await sut.UpdateAsync(activity.Id, request, Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ActivityScheduleRequired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_returns_thumbnail_not_found_when_missing()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(false);

        var result = await sut.UpdateAsync(activity.Id, UpdateRequest(), Guid.NewGuid());

        result.Error!.Code.Should().Be(ErrorCode.ActivityThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_returns_modality_not_found_when_missing()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(false);

        var result = await sut.UpdateAsync(activity.Id, UpdateRequest(), Guid.NewGuid());

        result.Error!.Code.Should().Be(ErrorCode.ActivityModalityTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_returns_role_type_not_found_when_allowed_role_unknown()
    {
        var activity = NewActivity();
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);
        roleTypes
            .CountAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        var roles = new List<ActivityAllowedRoleRequest> { new(Guid.NewGuid()) };

        var result = await sut.UpdateAsync(
            activity.Id,
            UpdateRequest(roles: roles),
            Guid.NewGuid()
        );

        result.Error!.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_mutates_clears_roles_and_persists()
    {
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var activity = NewActivity(title: "Old", id: activityId, eventId: eventId);
        activity.AllowedRoleTypes.Add(new ActivityAllowedRoleType { ActivityRoleTypeId = Guid.NewGuid() });

        var stored = new List<Activity> { activity };
        activities.Query().Returns(_ => stored.AsQueryable());
        activities.GetForEditAsync(activityId, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);

        var result = await sut.UpdateAsync(activityId, UpdateRequest(title: "  New  "), caller);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("New");
        activity.Title.Should().Be("New");
        activity.UpdatedBy.Should().Be(caller);
        activity.UpdatedAt.Should().Be(clock.UtcNow);
        activity.AllowedRoleTypes.Should().BeEmpty();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_replacing_thumbnail_cleans_up_the_previous_file_after_save()
    {
        var activity = NewActivity();
        var previousThumbnailId = activity.ThumbnailId;
        HasActivities(activity);
        activities.GetForEditAsync(activity.Id, Arg.Any<CancellationToken>()).Returns(activity);
        EventFound(NewEvent());
        ThumbnailExists(true);
        ModalityExists(true);

        var result = await sut.UpdateAsync(activity.Id, UpdateRequest(thumbnailId: Guid.NewGuid()), Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fileService.Received(1).DeleteIfOrphanedAsync(previousThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_keeping_the_same_thumbnail_does_not_clean_up()
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
            Guid.NewGuid()
        );

        result.IsSuccess.Should().BeTrue();
        await fileService.DidNotReceiveWithAnyArgs().DeleteIfOrphanedAsync(default, default);
    }

    // ---- DeleteAsync -------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_returns_not_found_when_activity_missing()
    {
        activities.FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Activity?)null);

        var result = await sut.DeleteAsync(Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await fileService.DidNotReceiveWithAnyArgs().DeleteIfOrphanedAsync(default, default);
    }

    [Fact]
    public async Task DeleteAsync_removes_saves_and_cleans_up_the_thumbnail()
    {
        var activity = NewActivity();
        activities.FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(activity);

        var result = await sut.DeleteAsync(activity.Id);

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).Remove(activity);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await fileService.Received(1).DeleteIfOrphanedAsync(activity.ThumbnailId, Arg.Any<CancellationToken>());
    }

    // ---- Catalog list methods ---------------------------------------------

    [Fact]
    public async Task ListRoleTypesAsync_orders_by_name_and_projects()
    {
        roleTypes.Query().Returns(
            new List<ActivityRoleType>
            {
                new() { Name = "Zeta", Description = "z" },
                new() { Name = "Alpha", Description = "a" },
            }.AsQueryable()
        );

        var result = await sut.ListRoleTypesAsync();

        result.Select(r => r.Name).Should().ContainInOrder("Alpha", "Zeta");
        result.Should().AllBeOfType<ActivityRoleTypeResponse>();
    }

    [Fact]
    public async Task ListAssignmentStatusTypesAsync_orders_by_name_and_projects()
    {
        statuses.Query().Returns(
            new List<AssignmentStatusType>
            {
                new() { Name = "Confirmado", Description = "c", Color = "#0f0" },
                new() { Name = "Aprobado", Description = "a", Color = "#00f" },
            }.AsQueryable()
        );

        var result = await sut.ListAssignmentStatusTypesAsync();

        result.Select(s => s.Name).Should().ContainInOrder("Aprobado", "Confirmado");
        result.Should().AllBeOfType<AssignmentStatusTypeResponse>();
    }

    [Fact]
    public async Task ListModalityTypesAsync_orders_by_name_and_projects()
    {
        modalityTypes.Query().Returns(
            new List<ActivityModalityType>
            {
                new() { Name = "Presencial" },
                new() { Name = "Online" },
            }.AsQueryable()
        );

        var result = await sut.ListModalityTypesAsync();

        result.Select(m => m.Name).Should().ContainInOrder("Online", "Presencial");
        result.Should().AllBeOfType<ActivityModalityTypeResponse>();
    }

    [Fact]
    public async Task ListAssignedAsync_filters_by_user_and_orders_by_start()
    {
        var userId = Guid.NewGuid();
        activities.QueryAssignments().Returns(
            new List<ActivityUserRoleAssignment>
            {
                Assignment(userId, "Late", new DateTimeOffset(2026, 7, 10, 14, 0, 0, TimeSpan.Zero)),
                Assignment(userId, "Early", new DateTimeOffset(2026, 7, 10, 9, 0, 0, TimeSpan.Zero)),
                Assignment(Guid.NewGuid(), "Other", new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)),
            }.AsQueryable()
        );

        var result = await sut.ListAssignedAsync(userId);

        result.Select(a => a.Title).Should().ContainInOrder("Early", "Late");
        result.Should().HaveCount(2);
    }

    private static ActivityUserRoleAssignment Assignment(Guid userId, string title, DateTimeOffset startsAt) =>
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

    // ---- ActivityRoleType CRUD --------------------------------------------

    [Fact]
    public async Task CreateActivityRoleTypeAsync_returns_conflict_when_name_exists()
    {
        roleTypes
            .ExistsAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await sut.CreateActivityRoleTypeAsync(new CreateActivityRoleTypeRequest("Líder", null));

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNameAlreadyExists);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateActivityRoleTypeAsync_trims_and_persists()
    {
        roleTypes
            .ExistsAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.CreateActivityRoleTypeAsync(
            new CreateActivityRoleTypeRequest("  Líder  ", "  Guía  ")
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Líder");
        result.Value.Description.Should().Be("Guía");
        await roleTypes.Received(1).AddAsync(
            Arg.Is<ActivityRoleType>(r => r.Name == "Líder" && r.Description == "Guía"),
            Arg.Any<CancellationToken>()
        );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateActivityRoleTypeAsync_defaults_null_description_to_empty()
    {
        roleTypes
            .ExistsAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.CreateActivityRoleTypeAsync(new CreateActivityRoleTypeRequest("Ayudante", null));

        result.Value.Description.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateActivityRoleTypeAsync_returns_not_found_when_missing()
    {
        roleTypes.FindAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((ActivityRoleType?)null);

        var result = await sut.UpdateActivityRoleTypeAsync(
            Guid.NewGuid(),
            new UpdateActivityRoleTypeRequest("Líder", null)
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateActivityRoleTypeAsync_returns_conflict_when_name_taken_by_other()
    {
        var id = Guid.NewGuid();
        roleTypes.FindAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new ActivityRoleType { Id = id, Name = "Old", Description = "d" });
        roleTypes.ExistsAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await sut.UpdateActivityRoleTypeAsync(
            id,
            new UpdateActivityRoleTypeRequest("Taken", null)
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNameAlreadyExists);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateActivityRoleTypeAsync_mutates_and_persists()
    {
        var id = Guid.NewGuid();
        var roleType = new ActivityRoleType { Id = id, Name = "Old", Description = "old" };
        roleTypes.FindAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(roleType);
        roleTypes.ExistsAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.UpdateActivityRoleTypeAsync(
            id,
            new UpdateActivityRoleTypeRequest("  New  ", "  Desc  ")
        );

        result.IsSuccess.Should().BeTrue();
        roleType.Name.Should().Be("New");
        roleType.Description.Should().Be("Desc");
        result.Value.Name.Should().Be("New");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteActivityRoleTypeAsync_returns_not_found_when_nothing_removed()
    {
        roleTypes.RemoveAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await sut.DeleteActivityRoleTypeAsync(Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task DeleteActivityRoleTypeAsync_saves_when_removed()
    {
        roleTypes.RemoveAsync(Arg.Any<Expression<Func<ActivityRoleType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await sut.DeleteActivityRoleTypeAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
