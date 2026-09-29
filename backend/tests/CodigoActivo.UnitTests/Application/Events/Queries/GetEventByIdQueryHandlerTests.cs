using AwesomeAssertions;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetEventByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetEventByIdQueryHandler sut;

    public GetEventByIdQueryHandlerTests()
    {
        sut = new GetEventByIdQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncEventExistsReturnsEvent()
    {
        var ev = NewEventRow();
        store.Events.Add(ev);

        var result = await sut.HandleAsync(
            new GetEventByIdQuery(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ev.Id);
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetEventByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
    }
}
