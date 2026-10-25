using CodigoActivo.Application.BlockedData;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// Purges the data history entries whose retention, set by
/// <see cref="DataHistoryRetentionOptions"/>, has ended. This is the only place that removes
/// entries.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
/// <param name="options">Retention of each entity.</param>
public sealed class DataHistoryRetention(
    CodigoActivoDbContext context,
    DataHistoryRetentionOptions options
) : IDataHistoryRetention
{
    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var specific = options.YearsByEntity.Keys.ToList();
        var purged = await context
            .DataHistory.Where(entry =>
                !specific.Contains(entry.Entity)
                && entry.OccurredAt <= now.AddYears(-options.DefaultYears)
            )
            .ExecuteDeleteAsync(ct);
        foreach (var (entity, years) in options.YearsByEntity)
        {
            purged += await context
                .DataHistory.Where(entry =>
                    entry.Entity == entity && entry.OccurredAt <= now.AddYears(-years)
                )
                .ExecuteDeleteAsync(ct);
        }

        return purged;
    }
}
