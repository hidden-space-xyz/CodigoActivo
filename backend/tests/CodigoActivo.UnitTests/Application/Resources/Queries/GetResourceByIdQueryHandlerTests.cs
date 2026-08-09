using AwesomeAssertions;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Queries;

public sealed class GetResourceByIdQueryHandlerTests
{
    private readonly IResourceRepository resources = Substitute.For<IResourceRepository>();
    private readonly GetResourceByIdQueryHandler sut;

    public GetResourceByIdQueryHandlerTests()
    {
        sut = new GetResourceByIdQueryHandler(
            resources,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncResourceExistsReturnsResource()
    {
        var resource = NewResource();
        resources.HasResources(resource);

        var result = await sut.HandleAsync(
            new GetResourceByIdQuery(resource.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(resource.Id);
        result.Value.Type.Id.Should().Be(resource.ResourceTypeId);
    }

    [Fact]
    public async Task HandleAsyncResourceMissingReturnsNotFound()
    {
        resources.HasResources();

        var result = await sut.HandleAsync(
            new GetResourceByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ResourceNotFound);
    }
}
