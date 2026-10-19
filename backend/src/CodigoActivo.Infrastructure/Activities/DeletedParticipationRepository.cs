using CodigoActivo.Domain.Activities;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Activities;

/// <summary>
/// Stores the blocked copies of removed participation and purges the ones whose retention ended.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public sealed class DeletedParticipationRepository(CodigoActivoDbContext context)
    : AggregateRepository<DeletedParticipation>(context),
        IDeletedParticipationRepository
{
    /// <inheritdoc />
    public Task<int> PurgeDeletedUpToAsync(
        DateTimeOffset deletedUpTo,
        CancellationToken ct = default
    )
    {
        return Set.Where(copy => copy.DeletedAt <= deletedUpTo).ExecuteDeleteAsync(ct);
    }
}
