using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Commands;

public sealed class CreateResourceCommandHandlerTests
{
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly IResourceTypeRepository resourceTypes =
        Substitute.For<IResourceTypeRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly CreateResourceCommandHandler sut;

    public CreateResourceCommandHandlerTests()
    {
        sut = new CreateResourceCommandHandler(
            resources,
            resourceTypes,
            files,
            clock,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncResourceTypeMissingReturnsBadRequestAndDoesNotPersist()
    {
        resourceTypes.TypeMissing();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            null,
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new CreateResourceCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceTypeNotFound);
        await resources
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<Resource>(), TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncInternalWithUrlReturnsBadRequest()
    {
        var type = resourceTypes.TypeExists();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new CreateResourceCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceUrlNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData(EmptyRichText)]
    public async Task HandleAsyncInternalWithEmptyDescriptionReturnsBadRequest(string? description)
    {
        var type = resourceTypes.TypeExists();
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            description,
            null,
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new CreateResourceCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceDescriptionRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncExternalWithDescriptionReturnsBadRequest()
    {
        var type = resourceTypes.TypeExists(isExternal: true);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            SomeRichText,
            "https://ejemplo.es",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new CreateResourceCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceDescriptionNotAllowed);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncExternalWithoutUrlReturnsBadRequest()
    {
        var type = resourceTypes.TypeExists(isExternal: true);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            null,
            "   ",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new CreateResourceCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceUrlRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsBadRequestAndDoesNotPersist()
    {
        var type = resourceTypes.TypeExists();
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
            new CreateResourceCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.ResourceThumbnailNotFound);
        await resources
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<Resource>(), TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidInternalRequestPersistsTrimmedResourceAndInvalidatesCache()
    {
        var type = resourceTypes.TypeExists();
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var request = new CreateResourceRequest(
            "  Title  ",
            "  Subtitle  ",
            SomeRichText,
            null,
            type.Id,
            thumbnailId
        );

        var result = await sut.HandleAsync(
            new CreateResourceCommand(request, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Title");
        result.Value.Subtitle.Should().Be("Subtitle");
        result.Value.Description.Should().Be(SomeRichText);
        result.Value.Url.Should().BeNull();
        result.Value.Type.Id.Should().Be(type.Id);
        result.Value.Type.IsExternal.Should().BeFalse();
        result.Value.ThumbnailId.Should().Be(thumbnailId);
        result.Value.CreatedBy.Should().Be(caller);
        result.Value.CreatedAt.Should().Be(clock.UtcNow);
        await resources
            .Received(1)
            .AddAsync(
                Arg.Is<Resource>(r =>
                    r != null
                    && r.Title == "Title"
                    && r.Subtitle == "Subtitle"
                    && r.CreatedBy == caller
                ),
                Arg.Any<CancellationToken>()
            );
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
    public async Task HandleAsyncValidExternalRequestPersistsTrimmedUrlAndEmptyDescription()
    {
        var type = resourceTypes.TypeExists(isExternal: true);
        files.ThumbnailExists(true);
        var request = new CreateResourceRequest(
            "Title",
            "Subtitle",
            null,
            "  https://ejemplo.es/curso  ",
            type.Id,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new CreateResourceCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Be("https://ejemplo.es/curso");
        result.Value.Description.Should().Be("{}");
        result.Value.Type.IsExternal.Should().BeTrue();
        await resources
            .Received(1)
            .AddAsync(
                Arg.Is<Resource>(r =>
                    r != null && r.Url == "https://ejemplo.es/curso" && r.Description == "{}"
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
