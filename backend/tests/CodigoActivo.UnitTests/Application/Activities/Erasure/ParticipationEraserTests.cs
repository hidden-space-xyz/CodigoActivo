using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities.Erasure;

public sealed class ParticipationEraserTests
{
    private readonly IParticipationErasureStore store =
        Substitute.For<IParticipationErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ParticipationDeletion deletion = ParticipationDeletion.OfEvent(EventId.New());
    private readonly ParticipationEraser sut;

    public ParticipationEraserTests()
    {
        sut = ParticipationErasers.Create(store, uow);
    }

    [Fact]
    public async Task EraseAsyncLoadsThenCommits()
    {
        await sut.EraseAsync(deletion, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            store.LoadAsync(deletion, Arg.Any<CancellationToken>());
            uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task EraseAsyncMissingDeletionThrowsBeforeLoading()
    {
        var withoutDeletion = () => sut.EraseAsync(null!, TestContext.Current.CancellationToken);

        await withoutDeletion.Should().ThrowAsync<ArgumentNullException>();
        await store
            .DidNotReceiveWithAnyArgs()
            .LoadAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotSaveAsync();
    }
}
