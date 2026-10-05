using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Common;
using NSubstitute;
using NSubstitute.Extensions;

namespace CodigoActivo.UnitTests.TestSupport;

public static class UnitOfWorkStubs
{
    public static IUnitOfWork RunsTransactions(this IUnitOfWork uow)
    {
        return uow.RunsTransactionsOf<bool>().RunsTransactionsOf<Result>();
    }

    private static IUnitOfWork RunsTransactionsOf<T>(this IUnitOfWork uow)
    {
        uow.Configure()
            .ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<T>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
                call.Arg<Func<CancellationToken, Task<T>>>()(call.Arg<CancellationToken>())
            );
        return uow;
    }
}
