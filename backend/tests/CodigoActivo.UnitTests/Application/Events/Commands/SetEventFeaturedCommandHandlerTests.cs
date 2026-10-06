using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
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
    private readonly SetEventFeaturedCommandHandler sut;

    public SetEventFeaturedCommandHandlerTests()
    {
        sut = new SetEventFeaturedCommandHandler(events, uow);
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        events.Finds(null);

        var result = await sut.HandleAsync(
            new SetEventFeaturedCommand(EventId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventNotFound);
    }

    [Fact]
    public async Task HandleAsyncEventExistsFeaturesTheEvent()
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
