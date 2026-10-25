using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities.Erasure;

namespace CodigoActivo.UnitTests.TestSupport;

public static class ParticipationErasers
{
    public static ParticipationEraser Create(IParticipationErasureStore store, IUnitOfWork uow)
    {
        return new ParticipationEraser(store, uow.RunsTransactions());
    }
}
