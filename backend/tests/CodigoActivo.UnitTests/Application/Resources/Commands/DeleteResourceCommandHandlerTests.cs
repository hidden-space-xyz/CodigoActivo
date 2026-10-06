using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Commands;

public sealed class DeleteResourceCommandHandlerTests
{
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly DeleteResourceCommandHandler sut;

    public DeleteResourceCommandHandlerTests()
    {
        sut = new DeleteResourceCommandHandler(resources);
    }

    [Fact]
    public async Task HandleAsyncResourceMissingReturnsNotFound()
    {
        resources.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteResourceCommand(ResourceId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ResourceNotFound);
        resources.DidNotReceiveWithAnyArgs().Remove(Arg.Any<Resource>());
    }

    [Fact]
    public async Task HandleAsyncResourceExistsRemovesItAndReleasesItsFiles()
    {
        var embeddedId = Guid.NewGuid();
        var resource = NewResource(description: $"{{\"img\":\"/api/files/{embeddedId}/content\"}}");
        resources.Finds(resource);

        var result = await sut.HandleAsync(
            new DeleteResourceCommand(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        resources.Received(1).Remove(resource);
        DomainEvents
            .ReleasedFiles(resource)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                ids != null
                && ids.Contains(StoredFileId.From(embeddedId))
                && ids.Contains(resource.ThumbnailId)
            );
    }
}
