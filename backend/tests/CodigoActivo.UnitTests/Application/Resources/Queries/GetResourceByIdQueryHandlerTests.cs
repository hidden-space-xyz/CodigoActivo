using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Resources;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Resources.ResourceTestData;

namespace CodigoActivo.UnitTests.Application.Resources.Queries;

public sealed class GetResourceByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetResourceByIdQueryHandler sut;

    public GetResourceByIdQueryHandlerTests()
    {
        sut = new GetResourceByIdQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncResourceExistsReturnsResource()
    {
        var resource = NewResourceRow();
        store.Resources.Add(resource);

        var result = await sut.HandleAsync(
            new GetResourceByIdQuery(ResourceId.From(resource.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(resource.Id);
        result.Value.Type.Id.Should().Be(resource.ResourceTypeId);
    }

    [Fact]
    public async Task HandleAsyncResourceMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetResourceByIdQuery(ResourceId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ResourceNotFound);
    }
}
