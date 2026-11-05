using CodigoActivo.Application.BlockedData;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// Purges the data history entries whose retention, set by
/// <see cref="DataHistoryRetentionOptions"/>, has ended, except those about a held account. This
/// is the only place that removes entries.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
/// <param name="options">Retention of each entity.</param>
public sealed class DataHistoryRetention(
    CodigoActivoDbContext context,
    DataHistoryRetentionOptions options
) : IDataHistoryRetention
{
    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync(
        DateTimeOffset now,
        IReadOnlyCollection<Guid> heldAccountIds,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(heldAccountIds);

        var held = heldAccountIds.ToArray();
        var unheld = context.DataHistory.Where(entry =>
            !entry.SubjectIds.Any(subject => held.Contains(subject))
        );
        var specific = options.YearsByEntity.Keys.ToList();
        var purged = await unheld
            .Where(entry =>
                !specific.Contains(entry.Entity)
                && entry.OccurredAt <= now.AddYears(-options.DefaultYears)
            )
            .ExecuteDeleteAsync(ct);
        foreach (var (entity, years) in options.YearsByEntity)
        {
            purged += await unheld
                .Where(entry => entry.Entity == entity && entry.OccurredAt <= now.AddYears(-years))
                .ExecuteDeleteAsync(ct);
        }

        return purged;
    }
}
