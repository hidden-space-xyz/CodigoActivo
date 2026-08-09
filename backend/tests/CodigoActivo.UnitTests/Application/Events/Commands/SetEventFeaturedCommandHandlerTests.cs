using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class SetEventFeaturedCommandHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly SetEventFeaturedCommandHandler sut;

    public SetEventFeaturedCommandHandlerTests()
    {
        sut = new SetEventFeaturedCommandHandler(
            events,
            cacheInvalidator,
            new GetEventByIdQueryHandler(events, new FakeQueryExecutor(), new FakeHybridCache())
        );
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.SetFeaturedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await sut.HandleAsync(
            new SetEventFeaturedCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task HandleAsyncEventExistsReturnsFeaturedEventAndInvalidatesCache()
    {
        var ev = NewEvent(featured: true);
        events.HasEvents(ev);
        events.SetFeaturedAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await sut.HandleAsync(
            new SetEventFeaturedCommand(ev.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ev.Id);
        result.Value.Featured.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Events)
                )
            );
    }
}
