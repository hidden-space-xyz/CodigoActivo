using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class UpdateEventCommandHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdateEventCommandHandler sut;

    public UpdateEventCommandHandlerTests()
    {
        sut = new UpdateEventCommandHandler(
            events,
            activities,
            files,
            orphanCleaner,
            new EventCategoryChecker(categoryTypes),
            clock,
            uow,
            cacheInvalidator,
            new GetEventByIdQueryHandler(events, new FakeQueryExecutor(), new FakeHybridCache())
        );
    }

    private void PrepareUpdate(Event ev)
    {
        events.HasEvents(ev);
        events.GetForEditAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                foreach (var category in ev.Categories)
                {
                    category.EventCategoryType ??= new EventCategoryType
                    {
                        Id = category.EventCategoryTypeId,
                        Name = "Charlas",
                        Color = "#654321",
                    };
                }

                return 1;
            });
    }

    [Fact]
    public async Task HandleAsync_InvalidScheduleRange_ReturnsErrorBeforeTouchingRepository()
    {
        var request = UpdateReq(
            eventStart: new DateOnly(2026, 8, 5),
            eventEnd: new DateOnly(2026, 8, 1),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            new UpdateEventCommand(Guid.NewGuid(), request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventScheduleInvalidRange);
        await events
            .DidNotReceiveWithAnyArgs()
            .GetForEditAsync(Guid.Empty, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_NoCategoriesSupplied_ReturnsCategoriesRequired()
    {
        var request = UpdateReq(categoryTypeIds: null);

        var result = await sut.HandleAsync(
            new UpdateEventCommand(Guid.NewGuid(), request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventCategoriesRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_EventMissing_ReturnsNotFound()
    {
        Event? missing = null;
        categoryTypes.HasCategoryCount(1);
        events.GetForEditAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(missing);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.HandleAsync(
            new UpdateEventCommand(Guid.NewGuid(), request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ActivityOutsideNewRange_ReturnsActivitiesOutsideRange()
    {
        var ev = NewEvent();
        categoryTypes.HasCategoryCount(1);
        events.GetForEditAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        activities
            .AnyOutsideRangeAsync(
                ev.Id,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.HandleAsync(
            new UpdateEventCommand(ev.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventActivitiesOutsideNewRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ThumbnailMissing_ReturnsThumbnailNotFound()
    {
        var ev = NewEvent();
        categoryTypes.HasCategoryCount(1);
        events.GetForEditAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        activities
            .AnyOutsideRangeAsync(
                ev.Id,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);
        files.ThumbnailExists(false);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.HandleAsync(
            new UpdateEventCommand(ev.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.EventThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_ReplacesCategoriesPersistsAndInvalidatesCache()
    {
        var caller = Guid.NewGuid();
        var newCategoryId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var ev = NewEvent("Old title", "Old subtitle");
        ev.Categories.Add(
            new EventCategory { EventId = ev.Id, EventCategoryTypeId = Guid.NewGuid() }
        );
        PrepareUpdate(ev);

        var request = UpdateReq(
            categoryTypeIds: [newCategoryId],
            thumbnailId: thumbnailId,
            title: "  New title  "
        );

        var result = await sut.HandleAsync(
            new UpdateEventCommand(ev.Id, request, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        ev.Title.Should().Be("New title");
        ev.Subtitle.Should().Be("New subtitle");
        ev.ThumbnailId.Should().Be(thumbnailId);
        ev.UpdatedBy.Should().Be(caller);
        ev.UpdatedAt.Should().Be(clock.UtcNow);
        ev.Categories.Should().ContainSingle().Which.EventCategoryTypeId.Should().Be(newCategoryId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Events)
                )
            );
    }

    [Fact]
    public async Task HandleAsync_SameThumbnail_DoesNotCleanUp()
    {
        var ev = NewEvent();
        PrepareUpdate(ev);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()], thumbnailId: ev.ThumbnailId);

        var result = await sut.HandleAsync(
            new UpdateEventCommand(ev.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Count == 0),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsync_ThumbnailReplaced_IncludesPreviousThumbnailInOrphanBatch()
    {
        var ev = NewEvent();
        var previousThumbnailId = ev.ThumbnailId;
        PrepareUpdate(ev);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()], thumbnailId: Guid.NewGuid());

        var result = await sut.HandleAsync(
            new UpdateEventCommand(ev.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids != null && ids.Count == 1 && ids.Contains(previousThumbnailId)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsync_UnchangedCategories_KeepsExistingCategoryInstances()
    {
        var keptA = Guid.NewGuid();
        var keptB = Guid.NewGuid();
        var ev = NewEvent();
        var categoryA = NewCategory(ev.Id, keptA, "Talleres", "#111111");
        var categoryB = NewCategory(ev.Id, keptB, "Charlas", "#222222");
        ev.Categories.Add(categoryA);
        ev.Categories.Add(categoryB);
        PrepareUpdate(ev);
        categoryTypes.HasCategoryCount(2);
        var request = UpdateReq(categoryTypeIds: [keptA, keptB], thumbnailId: ev.ThumbnailId);

        var result = await sut.HandleAsync(
            new UpdateEventCommand(ev.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        ev.Categories.Should().HaveCount(2);
        ev.Categories.Should().Contain(categoryA);
        ev.Categories.Should().Contain(categoryB);
    }

    [Fact]
    public async Task HandleAsync_ImagesDroppedFromDescription_CleansUpRemovedKeepsRest()
    {
        var ev = NewEvent();
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        ev.Description =
            $"{{\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}";
        PrepareUpdate(ev);
        var request = UpdateReq(
            categoryTypeIds: [Guid.NewGuid()],
            thumbnailId: ev.ThumbnailId,
            description: $"{{\"b\":\"/api/files/{keptId}/content\"}}"
        );

        var result = await sut.HandleAsync(
            new UpdateEventCommand(ev.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids != null && ids.Contains(removedId) && !ids.Contains(keptId)
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
