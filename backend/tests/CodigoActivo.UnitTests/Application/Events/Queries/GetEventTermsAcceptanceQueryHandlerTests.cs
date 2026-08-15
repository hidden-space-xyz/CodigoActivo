using AwesomeAssertions;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetEventTermsAcceptanceQueryHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly GetEventTermsAcceptanceQueryHandler sut;

    public GetEventTermsAcceptanceQueryHandlerTests()
    {
        sut = new GetEventTermsAcceptanceQueryHandler(events, new FakeQueryExecutor());
    }

    private Event EventWithTerms(Guid? termsDocumentId)
    {
        var ev = NewEvent();
        ev.TermsDocumentId = termsDocumentId;
        events.Query().Returns(new List<Event> { ev }.AsQueryable());
        return ev;
    }

    [Fact]
    public async Task HandleAsyncAcceptanceForCurrentDocumentReturnsAccepted()
    {
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        var ev = EventWithTerms(termsDocumentId);
        events
            .TermsAcceptanceExistsAsync(
                ev.Id,
                userId,
                termsDocumentId,
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var result = await sut.HandleAsync(
            new GetEventTermsAcceptanceQuery(ev.Id, userId),
            TestContext.Current.CancellationToken
        );

        result.Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncAcceptanceForOtherUserReturnsNotAccepted()
    {
        var ev = EventWithTerms(Guid.NewGuid());

        var result = await sut.HandleAsync(
            new GetEventTermsAcceptanceQuery(ev.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Accepted.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncEventWithoutTermsReturnsNotAccepted()
    {
        var ev = EventWithTerms(null);

        var result = await sut.HandleAsync(
            new GetEventTermsAcceptanceQuery(ev.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Accepted.Should().BeFalse();
        await events
            .DidNotReceiveWithAnyArgs()
            .TermsAcceptanceExistsAsync(
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task HandleAsyncAcceptanceForOldDocumentReturnsNotAccepted()
    {
        var userId = Guid.NewGuid();
        var ev = EventWithTerms(Guid.NewGuid());
        events
            .TermsAcceptanceExistsAsync(
                ev.Id,
                userId,
                Arg.Is<Guid>(id => id != ev.TermsDocumentId),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var result = await sut.HandleAsync(
            new GetEventTermsAcceptanceQuery(ev.Id, userId),
            TestContext.Current.CancellationToken
        );

        result.Accepted.Should().BeFalse();
    }
}
