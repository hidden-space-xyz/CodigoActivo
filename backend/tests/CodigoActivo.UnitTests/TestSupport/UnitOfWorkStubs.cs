using CodigoActivo.Application.Abstractions.Persistence;
using NSubstitute;
using NSubstitute.Extensions;

namespace CodigoActivo.UnitTests.TestSupport;

public static class UnitOfWorkStubs
{
    public static IUnitOfWork RunsTransactions(this IUnitOfWork uow)
    {
        uow.Configure()
            .ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
                call.Arg<Func<CancellationToken, Task<bool>>>()(call.Arg<CancellationToken>())
            );
        return uow;
    }
}
