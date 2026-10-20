using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Domain.Activities;
using NSubstitute;

namespace CodigoActivo.UnitTests.TestSupport;

public static class ParticipationErasers
{
    public const string Copy = "{\"event\":{}}";

    public static ParticipationEraser Create(
        IParticipationArchive archive,
        IDeletedParticipationRepository deletedParticipations,
        IUnitOfWork uow
    )
    {
        archive
            .CaptureCopyAsync(Arg.Any<ParticipationDeletion>(), Arg.Any<CancellationToken>())
            .Returns(Copy);
        return new ParticipationEraser(archive, deletedParticipations, uow.RunsTransactions());
    }
}
