using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Commands;

public sealed class UpdateResourceCommandHandlerTests
{
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly IResourceTypeRepository resourceTypes =
        Substitute.For<IResourceTypeRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdateResourceCommandHandler sut;

    public UpdateResourceCommandHandlerTests()
    {
        sut = new UpdateResourceCommandHandler(
            resources,
            resourceTypes,
            files,
            orphanCleaner,
            clock,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncResourceMissingReturnsNotFound()
    {
        resources.Finds(null);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(Guid.NewGuid(), request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ResourceNotFound);
        await files
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(
                Arg.Any<Expression<Func<FileEntity, bool>>>(),
                TestContext.Current.CancellationToken
            );
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncResourceTypeMissingReturnsBadRequest()
    {
        var resource = NewResource();
        resources.Finds(resource);
        resourceTypes.TypeMissing();
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            Guid.NewGuid(),
            resource.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncInternalWithUrlReturnsBadRequest()
    {
        var resource = NewResource();
        resources.Finds(resource);
        var type = resourceTypes.TypeExists();
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceUrlNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncExternalWithDescriptionReturnsBadRequest()
    {
        var resource = NewResource();
        resources.Finds(resource);
        var type = resourceTypes.TypeExists(isExternal: true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceDescriptionNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsBadRequest()
    {
        var resource = NewResource();
        resources.Finds(resource);
        var type = resourceTypes.TypeExists();
        files.ThumbnailExists(false);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestMutatesPersistsResourceAndInvalidatesCache()
    {
        var resource = NewResource("Old", "OldSub");
        resources.Finds(resource);
        var type = resourceTypes.TypeExists();
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        const string NewDescription =
            "{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"text\":\"Nuevo\"}]}";
        var request = new UpdateResourceRequest(
            "  New  ",
            "  NewSub  ",
            NewDescription,
            null,
            type.Id,
            thumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resource.Title.Should().Be("New");
        resource.Subtitle.Should().Be("NewSub");
        resource.Description.Should().Be(NewDescription);
        resource.ResourceTypeId.Should().Be(type.Id);
        resource.ThumbnailId.Should().Be(thumbnailId);
        resource.UpdatedBy.Should().Be(caller);
        resource.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Resources)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncSwitchToExternalClearsDescriptionAndCleansEmbeddedImages()
    {
        var resource = NewResource();
        var embeddedId = Guid.NewGuid();
        resource.Description =
            $"{{\"text\":\"cuerpo\",\"img\":\"/api/files/{embeddedId}/content\"}}";
        resources.Finds(resource);
        var type = resourceTypes.TypeExists(isExternal: true);
        files.ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            null,
            "https://ejemplo.es/curso",
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resource.Description.Should().Be("{}");
        resource.Url.Should().Be("https://ejemplo.es/curso");
        resource.ResourceTypeId.Should().Be(type.Id);
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(embeddedId)),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncSwitchToInternalClearsUrl()
    {
        var resource = NewResource();
        resource.Description = "{}";
        resource.Url = "https://ejemplo.es/antiguo";
        resources.Finds(resource);
        var type = resourceTypes.TypeExists();
        files.ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resource.Url.Should().BeNull();
        resource.Description.Should().Be(SomeRichText);
    }

    [Fact]
    public async Task HandleAsyncThumbnailReplacedCleansUpPreviousThumbnailAfterSave()
    {
        var resource = NewResource();
        var previousThumbnailId = resource.ThumbnailId;
        resources.Finds(resource);
        var type = resourceTypes.TypeExists();
        files.ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, Guid.NewGuid()),
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
    public async Task HandleAsyncThumbnailUnchangedDoesNotCleanUpThumbnail()
    {
        var resource = NewResource();
        resource.Description = SomeRichText;
        resources.Finds(resource);
        var type = resourceTypes.TypeExists();
        files.ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, Guid.NewGuid()),
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
    public async Task HandleAsyncImageRemovedFromDescriptionCleansUpRemovedImageOnly()
    {
        var resource = NewResource();
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        resource.Description =
            $"{{\"text\":\"cuerpo\",\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}";
        resources.Finds(resource);
        var type = resourceTypes.TypeExists();
        files.ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            $"{{\"text\":\"cuerpo\",\"b\":\"/api/files/{keptId}/content\"}}",
            null,
            type.Id,
            resource.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdateResourceCommand(resource.Id, request, Guid.NewGuid()),
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
