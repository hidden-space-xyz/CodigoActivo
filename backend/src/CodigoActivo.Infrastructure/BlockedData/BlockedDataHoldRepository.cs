using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.BlockedData;

/// <summary>
/// Stores the holds on the blocked data and purges the lifted ones whose retention ended.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public sealed class BlockedDataHoldRepository(CodigoActivoDbContext context)
    : AggregateRepository<BlockedDataHold>(context),
        IBlockedDataHoldRepository
{
    /// <inheritdoc />
    public Task<BlockedDataHold?> GetByIdAsync(BlockedDataHoldId id, CancellationToken ct = default)
    {
        return Set.SingleOrDefaultAsync(h => h.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserId>> ListHeldAccountIdsAsync(CancellationToken ct = default)
    {
        return await Set.Where(h => h.LiftedAt == null)
            .SelectMany(h => h.Subjects)
            .Select(s => s.AccountId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<int> PurgeLiftedUpToAsync(DateTimeOffset liftedUpTo, CancellationToken ct = default)
    {
        return Set.Where(h => h.LiftedAt <= liftedUpTo).ExecuteDeleteAsync(ct);
    }
}
