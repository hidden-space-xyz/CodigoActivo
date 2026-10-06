using AwesomeAssertions;
using CodigoActivo.API.Resources.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Application.Resources.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Commands;

public sealed class UpdateResourceCommandHandlerTests
{
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly FakeReadStore readStore = new();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly UpdateResourceCommandHandler sut;

    public UpdateResourceCommandHandlerTests()
    {
        sut = new UpdateResourceCommandHandler(resources, files, currentUser, clock);
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
            request.ToCommand(ResourceId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ResourceNotFound);
        await files
            .DidNotReceiveWithAnyArgs()
            .ExistsAsync(Arg.Any<StoredFileId>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncResourceTypeMissingReturnsBadRequest()
    {
        var resource = NewResource();
        resources.Finds(resource);
        readStore.TypeMissing();
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            Guid.NewGuid(),
            resource.ThumbnailId.Value
        );

        var result = await sut.HandleAsync(
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ResourceTypeNotFound);
    }

    [Fact]
    public async Task HandleAsyncInternalWithUrlReturnsBadRequest()
    {
        var resource = NewResource();
        resources.Finds(resource);
        var type = readStore.TypeExists();
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            resource.ThumbnailId.Value
        );

        var result = await sut.HandleAsync(
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ResourceUrlNotAllowed);
    }

    [Fact]
    public async Task HandleAsyncExternalWithDescriptionReturnsBadRequest()
    {
        var resource = NewResource();
        resources.Finds(resource);
        var type = readStore.TypeExists(isExternal: true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            resource.ThumbnailId.Value
        );

        var result = await sut.HandleAsync(
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ResourceDescriptionNotAllowed);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsBadRequest()
    {
        var resource = NewResource();
        resources.Finds(resource);
        var type = readStore.TypeExists();
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
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ResourceThumbnailNotFound);
    }

    [Fact]
    public async Task HandleAsyncValidRequestReplacesResourceByTheCurrentUser()
    {
        var resource = NewResource("Old", "OldSub");
        resources.Finds(resource);
        var type = readStore.TypeExists();
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        currentUser.Id = UserId.From(caller);
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
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resource.Title.Should().Be("New");
        resource.Subtitle.Should().Be("NewSub");
        resource.Description!.Json.Should().Be(NewDescription);
        resource.ResourceType.Should().Be(CatalogIds.ResourceTypes.ValueOf(type.Id));
        resource.ThumbnailId.Value.Should().Be(thumbnailId);
        resource.UpdatedBy.Should().Be(UserId.From(caller));
        resource.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncSwitchToExternalClearsDescriptionAndReleasesEmbeddedImages()
    {
        var embeddedId = Guid.NewGuid();
        var resource = NewResource(
            description: $"{{\"text\":\"cuerpo\",\"img\":\"/api/files/{embeddedId}/content\"}}"
        );
        resources.Finds(resource);
        var type = readStore.TypeExists(isExternal: true);
        files.ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            null,
            "https://ejemplo.es/curso",
            type.Id,
            resource.ThumbnailId.Value
        );

        var result = await sut.HandleAsync(
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resource.Description!.Json.Should().Be("{}");
        resource.Url.Should().Be("https://ejemplo.es/curso");
        resource.ResourceType.Should().Be(CatalogIds.ResourceTypes.ValueOf(type.Id));
        DomainEvents
            .ReleasedFiles(resource)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                ids != null && ids.Contains(StoredFileId.From(embeddedId))
            );
    }

    [Fact]
    public async Task HandleAsyncSwitchToInternalClearsUrl()
    {
        var resource = NewResource(url: "https://ejemplo.es/antiguo", description: "{}");
        resources.Finds(resource);
        var type = readStore.TypeExists();
        files.ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            resource.ThumbnailId.Value
        );

        var result = await sut.HandleAsync(
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resource.Url.Should().BeNull();
        resource.Description!.Json.Should().Be(SomeRichText);
    }

    [Fact]
    public async Task HandleAsyncThumbnailReplacedReleasesPreviousThumbnail()
    {
        var resource = NewResource();
        var previousThumbnailId = resource.ThumbnailId;
        resources.Finds(resource);
        var type = readStore.TypeExists();
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
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        DomainEvents
            .ReleasedFiles(resource)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                ids != null && ids.Count == 1 && ids.Contains(previousThumbnailId)
            );
    }

    [Fact]
    public async Task HandleAsyncThumbnailUnchangedReleasesNothing()
    {
        var resource = NewResource(description: SomeRichText);
        resources.Finds(resource);
        var type = readStore.TypeExists();
        files.ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            resource.ThumbnailId.Value
        );

        var result = await sut.HandleAsync(
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        DomainEvents
            .ReleasedFiles(resource)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids => ids != null && ids.Count == 0);
    }

    [Fact]
    public async Task HandleAsyncImageRemovedFromDescriptionReleasesRemovedImageOnly()
    {
        var removedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        var resource = NewResource(
            description: $"{{\"text\":\"cuerpo\",\"a\":\"/api/files/{removedId}/content\",\"b\":\"/api/files/{keptId}/content\"}}"
        );
        resources.Finds(resource);
        var type = readStore.TypeExists();
        files.ThumbnailExists(true);
        var request = new UpdateResourceRequest(
            "Title",
            "Subtitle",
            $"{{\"text\":\"cuerpo\",\"b\":\"/api/files/{keptId}/content\"}}",
            null,
            type.Id,
            resource.ThumbnailId.Value
        );

        var result = await sut.HandleAsync(
            request.ToCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        DomainEvents
            .ReleasedFiles(resource)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                ids != null
                && ids.Contains(StoredFileId.From(removedId))
                && !ids.Contains(StoredFileId.From(keptId))
            );
    }
}
