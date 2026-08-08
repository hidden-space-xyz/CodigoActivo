using AwesomeAssertions;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetEventByIdQueryHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly GetEventByIdQueryHandler sut;

    public GetEventByIdQueryHandlerTests()
    {
        sut = new GetEventByIdQueryHandler(events, new FakeQueryExecutor(), new FakeHybridCache());
    }

    [Fact]
    public async Task HandleAsync_EventExists_ReturnsEvent()
    {
        var ev = NewEvent();
        events.HasEvents(ev);

        var result = await sut.HandleAsync(
            new GetEventByIdQuery(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ev.Id);
    }

    [Fact]
    public async Task HandleAsync_EventMissing_ReturnsNotFound()
    {
        events.HasEvents();

        var result = await sut.HandleAsync(
            new GetEventByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
    }
}
