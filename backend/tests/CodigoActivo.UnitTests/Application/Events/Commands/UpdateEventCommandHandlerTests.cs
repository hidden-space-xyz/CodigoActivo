using AwesomeAssertions;
using CodigoActivo.API.Events.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
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
using CodigoActivo.Domain.Users;
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
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly UpdateEventCommandHandler sut;

    public UpdateEventCommandHandlerTests()
    {
        sut = new UpdateEventCommandHandler(
            events,
            activities,
            files,
            termsDocuments,
            new EventCategoryChecker(categoryTypes),
            currentUser,
            clock
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
            .CountExistingAsync(
                Arg.Any<IReadOnlyCollection<TermsDocumentId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(0);

        var result = await sut.HandleAsync(
            UpdateReq(
                    categoryTypeIds: [Guid.NewGuid()],
                    termsDocuments: [new EventTermsDocumentRequest(Guid.NewGuid())]
                )
                .ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentNotFound);
    }

    [Fact]
    public async Task HandleAsyncDuplicateTermsDocumentIdsReturnsTermsDocumentDuplicated()
    {
        var ev = NewEvent();
        var termsDocumentId = Guid.NewGuid();
        PrepareUpdate(ev);

        var result = await sut.HandleAsync(
            UpdateReq(
                    categoryTypeIds: [Guid.NewGuid()],
                    termsDocuments:
                    [
                        new EventTermsDocumentRequest(termsDocumentId),
                        new EventTermsDocumentRequest(termsDocumentId, Required: true),
                    ]
                )
                .ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.EventTermsDocumentDuplicated);
    }

    [Fact]
    public async Task HandleAsyncKnownTermsDocumentUpdatesTermsReference()
    {
        var ev = NewEvent();
        var termsDocumentId = Guid.NewGuid();
        PrepareUpdate(ev);
        termsDocuments
            .CountExistingAsync(
                Arg.Any<IReadOnlyCollection<TermsDocumentId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1);

        var result = await sut.HandleAsync(
            UpdateReq(
                    categoryTypeIds: [Guid.NewGuid()],
                    termsDocuments: [new EventTermsDocumentRequest(termsDocumentId, Required: true)]
                )
                .ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        ev.TermsDocuments.Should()
            .ContainSingle()
            .Which.TermsDocumentId.Value.Should()
            .Be(termsDocumentId);
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
            request.ToCommand(EventId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(DomainErrorCode.EventScheduleInvalidRange);
        await events
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(EventId.From(Guid.Empty), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncNoCategoriesSuppliedReturnsCategoriesRequired()
    {
        var request = UpdateReq(categoryTypeIds: null);

        var result = await sut.HandleAsync(
            request.ToCommand(EventId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(DomainErrorCode.EventCategoriesRequired);
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        categoryTypes.HasCategoryCount(1);
        events.Finds(null);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.HandleAsync(
            request.ToCommand(EventId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventNotFound);
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
            request.ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventActivitiesOutsideNewRange);
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
            request.ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ApplicationErrorCode.EventThumbnailNotFound);
    }

    [Fact]
    public async Task HandleAsyncValidRequestReplacesCategoriesStages()
    {
        var caller = Guid.NewGuid();
        currentUser.Id = UserId.From(caller);
        var newCategoryId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var ev = NewEvent(
            "Old title",
            "Old subtitle",
            categoryTypeIds: [EventCategoryTypeId.From(Guid.NewGuid())]
        );
        PrepareUpdate(ev);

        var request = UpdateReq(
            categoryTypeIds: [newCategoryId],
            thumbnailId: thumbnailId,
            title: "  New title  "
        );

        var result = await sut.HandleAsync(
            request.ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        ev.Title.Should().Be("New title");
        ev.Subtitle.Should().Be("New subtitle");
        ev.ThumbnailId.Value.Should().Be(thumbnailId);
        ev.UpdatedBy.Should().Be(UserId.From(caller));
        ev.UpdatedAt.Should().Be(clock.UtcNow);
        ev.Categories.Should()
            .ContainSingle()
            .Which.EventCategoryTypeId.Value.Should()
            .Be(newCategoryId);
    }

    [Fact]
    public async Task HandleAsyncSameThumbnailDoesNotCleanUp()
    {
        var ev = NewEvent();
        PrepareUpdate(ev);
        var request = UpdateReq(
            categoryTypeIds: [Guid.NewGuid()],
            thumbnailId: ev.ThumbnailId.Value
        );

        var result = await sut.HandleAsync(
            request.ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        DomainEvents
            .ReleasedFiles(ev)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids => ids != null && ids.Count == 0);
    }

    [Fact]
    public async Task HandleAsyncThumbnailReplacedIncludesPreviousThumbnailInOrphanBatch()
    {
        var ev = NewEvent();
        var previousThumbnailId = ev.ThumbnailId;
        PrepareUpdate(ev);
        var request = UpdateReq(categoryTypeIds: [Guid.NewGuid()], thumbnailId: Guid.NewGuid());

        var result = await sut.HandleAsync(
            request.ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        DomainEvents
            .ReleasedFiles(ev)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                ids != null && ids.Count == 1 && ids.Contains(previousThumbnailId)
            );
    }

    [Fact]
    public async Task HandleAsyncUnchangedCategoriesKeepsExistingCategoryInstances()
    {
        var keptA = Guid.NewGuid();
        var keptB = Guid.NewGuid();
        var ev = NewEvent(
            categoryTypeIds: [EventCategoryTypeId.From(keptA), EventCategoryTypeId.From(keptB)]
        );
        var categoryA = ev.Categories.Single(c =>
            c.EventCategoryTypeId == EventCategoryTypeId.From(keptA)
        );
        var categoryB = ev.Categories.Single(c =>
            c.EventCategoryTypeId == EventCategoryTypeId.From(keptB)
        );
        PrepareUpdate(ev);
        categoryTypes.HasCategoryCount(2);
        var request = UpdateReq(categoryTypeIds: [keptA, keptB], thumbnailId: ev.ThumbnailId.Value);

        var result = await sut.HandleAsync(
            request.ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        ev.Categories.Should().HaveCount(2);
        ev.Categories.Should().Contain(categoryA);
        ev.Categories.Should().Contain(categoryB);
    }

    [Fact]
    public async Task HandleAsyncImagesDroppedFromDescriptionReleasesRemovedKeepsRest()
    {
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        var ev = NewEvent(
            description: $"{{\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}"
        );
        PrepareUpdate(ev);
        var request = UpdateReq(
            categoryTypeIds: [Guid.NewGuid()],
            thumbnailId: ev.ThumbnailId.Value,
            description: $"{{\"b\":\"/api/files/{keptId}/content\"}}"
        );

        var result = await sut.HandleAsync(
            request.ToCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        DomainEvents
            .ReleasedFiles(ev)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                ids != null
                && ids.Contains(StoredFileId.From(removedId))
                && !ids.Contains(StoredFileId.From(keptId))
            );
    }
}
