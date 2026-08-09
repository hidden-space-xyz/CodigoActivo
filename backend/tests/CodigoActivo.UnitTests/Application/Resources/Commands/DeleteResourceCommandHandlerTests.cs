using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Commands;

public sealed class DeleteResourceCommandHandlerTests
{
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteResourceCommandHandler sut;

    public DeleteResourceCommandHandlerTests()
    {
        sut = new DeleteResourceCommandHandler(resources, orphanCleaner, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncResourceMissingReturnsNotFound()
    {
        resources.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteResourceCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ResourceNotFound);
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
    public async Task HandleAsyncResourceExistsRemovesSavesCleansUpFilesAndInvalidatesCache()
    {
        var resource = NewResource();
        var embeddedId = Guid.NewGuid();
        resource.Description = $"{{\"img\":\"/api/files/{embeddedId}/content\"}}";
        resources.Finds(resource);

        var result = await sut.HandleAsync(
            new DeleteResourceCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resources.Received(1).Remove(resource);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids != null && ids.Contains(embeddedId) && ids.Contains(resource.ThumbnailId)
                ),
                Arg.Any<CancellationToken>()
            );
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Resources)
                )
            );
    }
}
