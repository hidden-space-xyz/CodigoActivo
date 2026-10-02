using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class UpdateEventCommandHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
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
            termsDocuments,
            orphanCleaner,
            new EventCategoryChecker(categoryTypes),
            clock,
            uow,
            cacheInvalidator
        );
    }

    private void PrepareUpdate(Event ev)
    {
        events.GetByIdAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
    }

    [Fact]
    public async Task HandleAsyncUnknownTermsDocumentReturnsTermsDocumentNotFound()
    {
        var ev = NewEvent();
        PrepareUpdate(ev);
        termsDocuments
            .CountExistingAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await sut.HandleAsync(
            new UpdateEventCommand(
                ev.Id,
                UpdateReq(
                    categoryTypeIds: [Guid.NewGuid()],
                    termsDocuments: [new EventTermsDocumentRequest(Guid.NewGuid())]
                ),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.TermsDocumentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncDuplicateTermsDocumentIdsReturnsTermsDocumentDuplicated()
    {
        var ev = NewEvent();
        var termsDocumentId = Guid.NewGuid();
        PrepareUpdate(ev);

        var result = await sut.HandleAsync(
            new UpdateEventCommand(
                ev.Id,
                UpdateReq(
                    categoryTypeIds: [Guid.NewGuid()],
                    termsDocuments:
                    [
                        new EventTermsDocumentRequest(termsDocumentId),
                        new EventTermsDocumentRequest(termsDocumentId, Required: true),
                    ]
                ),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.EventTermsDocumentDuplicated);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncKnownTermsDocumentUpdatesTermsReference()
    {
        var ev = NewEvent();
        var termsDocumentId = Guid.NewGuid();
        PrepareUpdate(ev);
        termsDocuments
            .CountExistingAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await sut.HandleAsync(
            new UpdateEventCommand(
                ev.Id,
                UpdateReq(
                    categoryTypeIds: [Guid.NewGuid()],
                    termsDocuments: [new EventTermsDocumentRequest(termsDocumentId, Required: true)]
                ),
                Guid.NewGuid()
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        ev.TermsDocuments.Should()
            .ContainSingle()
            .Which.TermsDocumentId.Should()
            .Be(termsDocumentId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncInvalidScheduleRangeReturnsErrorBeforeTouchingRepository()
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
            .GetByIdAsync(Guid.Empty, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncNoCategoriesSuppliedReturnsCategoriesRequired()
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
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        categoryTypes.HasCategoryCount(1);
        events.Finds(null);
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
    public async Task HandleAsyncActivityOutsideNewRangeReturnsActivitiesOutsideRange()
    {
        var ev = NewEvent();
        categoryTypes.HasCategoryCount(1);
        events.GetByIdAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
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

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.EventActivitiesOutsideNewRange);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsThumbnailNotFound()
    {
        var ev = NewEvent();
        categoryTypes.HasCategoryCount(1);
        events.GetByIdAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
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
    public async Task HandleAsyncValidRequestReplacesCategoriesPersistsAndInvalidatesCache()
    {
        var caller = Guid.NewGuid();
        var newCategoryId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var ev = NewEvent("Old title", "Old subtitle", categoryTypeIds: [Guid.NewGuid()]);
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
    public async Task HandleAsyncSameThumbnailDoesNotCleanUp()
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
    public async Task HandleAsyncThumbnailReplacedIncludesPreviousThumbnailInOrphanBatch()
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
    public async Task HandleAsyncUnchangedCategoriesKeepsExistingCategoryInstances()
    {
        var keptA = Guid.NewGuid();
        var keptB = Guid.NewGuid();
        var ev = NewEvent(categoryTypeIds: [keptA, keptB]);
        var categoryA = ev.Categories.Single(c => c.EventCategoryTypeId == keptA);
        var categoryB = ev.Categories.Single(c => c.EventCategoryTypeId == keptB);
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
    public async Task HandleAsyncImagesDroppedFromDescriptionCleansUpRemovedKeepsRest()
    {
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        var ev = NewEvent(
            description: $"{{\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}"
        );
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
