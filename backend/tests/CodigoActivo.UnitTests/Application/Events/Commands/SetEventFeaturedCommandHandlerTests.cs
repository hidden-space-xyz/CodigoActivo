using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class SetEventFeaturedCommandHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>().RunsTransactions();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly SetEventFeaturedCommandHandler sut;

    public SetEventFeaturedCommandHandlerTests()
    {
        sut = new SetEventFeaturedCommandHandler(events, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.Finds(null);

        var result = await sut.HandleAsync(
            new SetEventFeaturedCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncEventExistsFeaturesTheEventAndInvalidatesCache()
    {
        var chosen = NewEvent();
        events.GetByIdAsync(chosen.Id, Arg.Any<CancellationToken>()).Returns(chosen);
        events.ListFeaturedAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await sut.HandleAsync(
            new SetEventFeaturedCommand(chosen.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        chosen.Featured.Should().BeTrue();
        await uow.Received(1)
            .ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>()
            );
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Events)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncOtherEventFeaturedUnfeaturesItWithoutTouchingAuditFields()
    {
        var chosen = NewEvent();
        var previous = NewEvent(featured: true);
        events.GetByIdAsync(chosen.Id, Arg.Any<CancellationToken>()).Returns(chosen);
        events.ListFeaturedAsync(Arg.Any<CancellationToken>()).Returns([previous]);

        var result = await sut.HandleAsync(
            new SetEventFeaturedCommand(chosen.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        chosen.Featured.Should().BeTrue();
        previous.Featured.Should().BeFalse();
        chosen.UpdatedAt.Should().BeNull();
        previous.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncOtherEventFeaturedSavesItsUnfeaturingBeforeFeaturingTheChosenOne()
    {
        var chosen = NewEvent();
        var previous = NewEvent(featured: true);
        events.GetByIdAsync(chosen.Id, Arg.Any<CancellationToken>()).Returns(chosen);
        events.ListFeaturedAsync(Arg.Any<CancellationToken>()).Returns([previous]);
        var saved = new List<(bool Previous, bool Chosen)>();
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => saved.Add((previous.Featured, chosen.Featured)));

        var result = await sut.HandleAsync(
            new SetEventFeaturedCommand(chosen.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        saved.Should().Equal((false, false), (false, true));
    }
}
