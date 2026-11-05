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
    public async Task<BlockedDataHold?> GetLockedAsync(
        BlockedDataHoldId id,
        CancellationToken ct = default
    )
    {
        FormattableString sql = $"""
            SELECT id AS "Value" FROM blocked_data_holds WHERE id = {id.Value} FOR UPDATE
            """;
        return (await Context.Database.SqlQuery<Guid>(sql).ToListAsync(ct)).Count is 0
            ? null
            : await Set.SingleAsync(h => h.Id == id, ct);
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
