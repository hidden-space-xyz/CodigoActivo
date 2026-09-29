using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Stores the legal copies of erased accounts and purges the ones whose retention ended.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public sealed class DeletedAccountRepository(CodigoActivoDbContext context)
    : AggregateRepository<DeletedAccount>(context),
        IDeletedAccountRepository
{
    /// <inheritdoc />
    public Task<int> RemoveDeletedUpToAsync(
        DateTimeOffset deletedUpTo,
        CancellationToken ct = default
    )
    {
        return Set.Where(copy => copy.DeletedAt <= deletedUpTo).ExecuteDeleteAsync(ct);
    }
}
