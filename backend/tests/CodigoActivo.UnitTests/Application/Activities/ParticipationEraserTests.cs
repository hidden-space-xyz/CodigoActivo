using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities;

public sealed class ParticipationEraserTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly IParticipationArchive archive = Substitute.For<IParticipationArchive>();
    private readonly IDeletedParticipationRepository deletedParticipations =
        Substitute.For<IDeletedParticipationRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ParticipationDeletion deletion = ParticipationDeletion.OfEvent(
        EventId.New(),
        UserId.New(),
        Now
    );
    private readonly ParticipationEraser sut;

    public ParticipationEraserTests()
    {
        sut = new ParticipationEraser(archive, deletedParticipations, uow.RunsTransactions());
    }

    [Fact]
    public async Task EraseAsyncLocksThenCopiesThenCommitsInOneTransaction()
    {
        archive
            .CaptureCopyAsync(deletion, Arg.Any<CancellationToken>())
            .Returns(ParticipationErasers.Copy);

        await sut.EraseAsync(deletion, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            uow.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>()
            );
            archive.LockAsync(deletion, Arg.Any<CancellationToken>());
            archive.CaptureCopyAsync(deletion, Arg.Any<CancellationToken>());
            deletedParticipations.AddAsync(
                Arg.Is<DeletedParticipation>(copy =>
                    copy.Data == ParticipationErasers.Copy && copy.DeletedAt == Now
                ),
                Arg.Any<CancellationToken>()
            );
            uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task EraseAsyncNothingToCopyCommitsTheRemovalWithoutACopy()
    {
        archive.CaptureCopyAsync(deletion, Arg.Any<CancellationToken>()).Returns((string?)null);

        await sut.EraseAsync(deletion, TestContext.Current.CancellationToken);

        await deletedParticipations
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<DeletedParticipation>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
