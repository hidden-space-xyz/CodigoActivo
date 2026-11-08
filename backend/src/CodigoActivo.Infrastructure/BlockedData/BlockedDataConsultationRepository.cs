using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.BlockedData;

/// <summary>
/// Stores the consultations of the blocked data and purges the ones whose retention ended.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public sealed class BlockedDataConsultationRepository(CodigoActivoDbContext context)
    : AggregateRepository<BlockedDataConsultation>(context),
        IBlockedDataConsultationRepository
{
    /// <inheritdoc />
    public Task<BlockedDataConsultation?> GetByIdAsync(
        BlockedDataConsultationId id,
        CancellationToken ct = default
    )
    {
        return Set.Include(c => c.Findings)
            .Include(c => c.Extracts)
            .SingleOrDefaultAsync(c => c.Id == id, ct);
    }

    /// <inheritdoc />
    public Task<int> PurgeOpenedUpToAsync(DateTimeOffset openedUpTo, CancellationToken ct = default)
    {
        return Set.Where(c => c.OpenedAt <= openedUpTo).ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlockedDataConsultation>> ListNoticesPendingAsync(
        int limit,
        CancellationToken ct = default
    )
    {
        return await Set.Where(c => c.NoticePending)
            .OrderBy(c => c.OpenedAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}
