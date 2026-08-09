using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
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
            new FakeQueryExecutor(),
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
        var foreignActivity = new Activity
        {
            Title = "Actividad de prueba",
            Location = "Sala principal",
            Description = "Descripción de la actividad",
            EventId = Guid.NewGuid(),
            ThumbnailId = Guid.NewGuid(),
        };
        activities
            .Query()
            .Returns(
                new[]
                {
                    new Activity
                    {
                        Title = "Actividad de prueba",
                        Location = "Sala principal",
                        Description = "Descripción de la actividad",
                        EventId = ev.Id,
                        ThumbnailId = sharedActivityThumbnailId,
                    },
                    new Activity
                    {
                        Title = "Actividad de prueba",
                        Location = "Sala principal",
                        Description = "Descripción de la actividad",
                        EventId = ev.Id,
                        ThumbnailId = sharedActivityThumbnailId,
                    },
                    foreignActivity,
                }.AsQueryable()
            );

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
                        foreignActivity.ThumbnailId
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
        var ev = NewEvent();
        var embeddedId = Guid.NewGuid();
        ev.Description = $"{{\"img\":\"/api/files/{embeddedId}/content\"}}";
        events.Finds(ev);
        activities.Query().Returns(Array.Empty<Activity>().AsQueryable());

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
