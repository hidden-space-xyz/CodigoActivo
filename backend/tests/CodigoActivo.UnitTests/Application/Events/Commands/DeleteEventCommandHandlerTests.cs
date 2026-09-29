using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class DeleteEventCommandHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteEventCommandHandler sut;

    public DeleteEventCommandHandlerTests()
    {
        sut = new DeleteEventCommandHandler(
            events,
            activities,
            orphanCleaner,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteEventCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await orphanCleaner
            .DidNotReceiveWithAnyArgs()
            .DeleteOrphanedAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                TestContext.Current.CancellationToken
            );
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task HandleAsyncValidEventRemovesCleansThumbnailsAndInvalidatesCache()
    {
        var ev = NewEvent();
        events.Finds(ev);
        var sharedActivityThumbnailId = Guid.NewGuid();
        var foreignThumbnailId = Guid.NewGuid();
        activities
            .ListThumbnailIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([foreignThumbnailId]);
        activities
            .ListThumbnailIdsAsync(ev.Id, Arg.Any<CancellationToken>())
            .Returns([sharedActivityThumbnailId, sharedActivityThumbnailId]);

        var result = await sut.HandleAsync(
            new DeleteEventCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        events.Received(1).Remove(ev);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    IsDeletedEventThumbnailBatch(
                        ids,
                        ev.ThumbnailId,
                        sharedActivityThumbnailId,
                        foreignThumbnailId
                    )
                ),
                Arg.Any<CancellationToken>()
            );
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null
                    && tags.Contains(CacheTags.Events)
                    && tags.Contains(CacheTags.Activities)
                )
            );
    }

    private static bool IsDeletedEventThumbnailBatch(
        IReadOnlyCollection<Guid>? ids,
        Guid eventThumbnailId,
        Guid activityThumbnailId,
        Guid foreignThumbnailId
    )
    {
        if (ids is null || ids.Count != 2)
        {
            return false;
        }

        var hasEventThumbnail = ids.Contains(eventThumbnailId);
        var hasActivityThumbnail = ids.Contains(activityThumbnailId);
        return hasEventThumbnail && hasActivityThumbnail && !ids.Contains(foreignThumbnailId);
    }

    [Fact]
    public async Task HandleAsyncImagesEmbeddedInDescriptionCleansThemUp()
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
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(embeddedId)),
                Arg.Any<CancellationToken>()
            );
    }
}
