using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class DeleteEventCommandHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly DeleteEventCommandHandler sut;

    public DeleteEventCommandHandlerTests()
    {
        sut = new DeleteEventCommandHandler(events, activities);
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteEventCommand(EventId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventNotFound);
        events.DidNotReceiveWithAnyArgs().Remove(Arg.Any<Event>());
    }

    [Fact]
    public async Task HandleAsyncValidEventRemovesItAndReleasesItsAndItsActivitiesThumbnails()
    {
        var ev = NewEvent();
        events.Finds(ev);
        var sharedActivityThumbnailId = Guid.NewGuid();
        var foreignThumbnailId = Guid.NewGuid();
        activities
            .ListThumbnailIdsAsync(Arg.Any<EventId>(), Arg.Any<CancellationToken>())
            .Returns([StoredFileId.From(foreignThumbnailId)]);
        activities
            .ListThumbnailIdsAsync(ev.Id, Arg.Any<CancellationToken>())
            .Returns([
                StoredFileId.From(sharedActivityThumbnailId),
                StoredFileId.From(sharedActivityThumbnailId),
            ]);

        var result = await sut.HandleAsync(
            new DeleteEventCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        events.Received(1).Remove(ev);
        DomainEvents
            .ReleasedFiles(ev)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                IsDeletedEventThumbnailBatch(
                    ids,
                    ev.ThumbnailId.Value,
                    sharedActivityThumbnailId,
                    foreignThumbnailId
                )
            );
    }

    private static bool IsDeletedEventThumbnailBatch(
        IReadOnlyCollection<StoredFileId>? ids,
        Guid eventThumbnailId,
        Guid activityThumbnailId,
        Guid foreignThumbnailId
    )
    {
        if (ids is null || ids.Count != 2)
        {
            return false;
        }

        var hasEventThumbnail = ids.Contains(StoredFileId.From(eventThumbnailId));
        var hasActivityThumbnail = ids.Contains(StoredFileId.From(activityThumbnailId));
        return hasEventThumbnail
            && hasActivityThumbnail
            && !ids.Contains(StoredFileId.From(foreignThumbnailId));
    }

    [Fact]
    public async Task HandleAsyncImagesEmbeddedInDescriptionReleasesThem()
    {
        var embeddedId = Guid.NewGuid();
        var ev = NewEvent(description: $"{{\"img\":\"/api/files/{embeddedId}/content\"}}");
        events.Finds(ev);
        activities.ListThumbnailIdsAsync(ev.Id, Arg.Any<CancellationToken>()).Returns([]);

        var result = await sut.HandleAsync(
            new DeleteEventCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        DomainEvents
            .ReleasedFiles(ev)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                ids != null && ids.Contains(StoredFileId.From(embeddedId))
            );
    }
}
