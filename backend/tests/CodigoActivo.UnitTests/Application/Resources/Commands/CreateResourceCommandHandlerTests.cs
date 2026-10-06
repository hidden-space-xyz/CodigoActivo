using AwesomeAssertions;
using CodigoActivo.API.Resources.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Errors;
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

public sealed class CreateResourceCommandHandlerTests
{
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly FakeReadStore readStore = new();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly CreateResourceCommandHandler sut;

    public CreateResourceCommandHandlerTests()
    {
        sut = new CreateResourceCommandHandler(resources, files, currentUser, clock);
    }

    private async Task<List<Resource>> CaptureAddedResourcesAsync()
    {
        var added = new List<Resource>();
        await resources.AddAsync(Arg.Do<Resource>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    [Fact]
    public async Task HandleAsyncResourceTypeMissingReturnsBadRequestAndDoesNotPersist()
    {
        readStore.TypeMissing();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ResourceTypeNotFound);
        await resources
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<Resource>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncInternalWithUrlReturnsBadRequest()
    {
        var type = readStore.TypeExists();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ResourceUrlNotAllowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData(EmptyRichText)]
    public async Task HandleAsyncInternalWithEmptyDescriptionReturnsBadRequest(string? description)
    {
        var type = readStore.TypeExists();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            description,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ResourceDescriptionRequired);
    }

    [Fact]
    public async Task HandleAsyncExternalWithDescriptionReturnsBadRequest()
    {
        var type = readStore.TypeExists(isExternal: true);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ResourceDescriptionNotAllowed);
    }

    [Fact]
    public async Task HandleAsyncExternalWithoutUrlReturnsBadRequest()
    {
        var type = readStore.TypeExists(isExternal: true);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            null,
            "   ",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.ResourceUrlRequired);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsBadRequestAndDoesNotPersist()
    {
        var type = readStore.TypeExists();
        files.ThumbnailExists(false);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.ResourceThumbnailNotFound);
        await resources
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<Resource>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidInternalRequestStagesTrimmedResource()
    {
        var type = readStore.TypeExists();
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        currentUser.Id = UserId.From(caller);
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var added = await CaptureAddedResourcesAsync();
        var request = new CreateResourceRequest(
            "  Title  ",
            "  Subtitle  ",
            SomeRichText,
            null,
            type.Id,
            thumbnailId
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Title.Should().Be("Title");
        created.Subtitle.Should().Be("Subtitle");
        created.Description!.Json.Should().Be(SomeRichText);
        created.Url.Should().BeNull();
        created.ResourceType.Should().Be(CatalogIds.ResourceTypes.ValueOf(type.Id));
        created.ThumbnailId.Value.Should().Be(thumbnailId);
        created.CreatedBy.Value.Should().Be(caller);
        created.CreatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncValidExternalRequestPersistsTrimmedUrlAndEmptyDescription()
    {
        var type = readStore.TypeExists(isExternal: true);
        files.ThumbnailExists(true);
        var added = await CaptureAddedResourcesAsync();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            null,
            "  https://ejemplo.es/curso  ",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Url.Should().Be("https://ejemplo.es/curso");
        created.Description!.Json.Should().Be("{}");
        created.ResourceType.Should().Be(CatalogIds.ResourceTypes.ValueOf(type.Id));
    }
}
