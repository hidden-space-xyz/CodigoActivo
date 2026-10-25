using System.Security.Cryptography;
using System.Text.Json;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Infrastructure.Database.Configurations;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// Reads and decrypts the data history for the consultations of the blocked data. An entry that
/// no longer decrypts, because it was altered or its key left the key ring, is returned without
/// content and logged, so an extract still tells it existed.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
/// <param name="protector">Protector that decrypts the records.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DataHistoryReader(
    CodigoActivoDbContext context,
    DataHistoryProtector protector,
    ILogger<DataHistoryReader> logger
) : IDataHistoryReader
{
    /// <inheritdoc />
    public IReadOnlyList<string> Entities =>
        [
            .. context
                .Model.GetEntityTypes()
                .Where(type => type.IsHistorized())
                .Select(type => type.ClrType.Name)
                .Order(StringComparer.Ordinal),
        ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataHistoryItem>> ReadEntityAsync(
        string entity,
        DateTimeOffset upTo,
        CancellationToken ct = default
    )
    {
        var entries = await context
            .DataHistory.AsNoTracking()
            .Where(entry => entry.Entity == entity && entry.OccurredAt <= upTo)
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .ToListAsync(ct);
        return [.. entries.Select(Read)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataHistoryItem>> ReadAboutAsync(
        IReadOnlyCollection<Guid> accountIds,
        DataHistoryPeriod period,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(accountIds);
        ArgumentNullException.ThrowIfNull(period);

        var ids = accountIds.ToArray();
        var query = context
            .DataHistory.AsNoTracking()
            .Where(entry =>
                entry.SubjectIds.Any(subject => ids.Contains(subject))
                && entry.OccurredAt <= period.UpTo
            );
        if (period.From is { } from)
        {
            query = query.Where(entry => entry.OccurredAt >= from);
        }

        if (period.Until is { } until)
        {
            query = query.Where(entry => entry.OccurredAt < until);
        }

        if (period.Entities.Count > 0)
        {
            var entities = period.Entities.ToList();
            query = query.Where(entry => entities.Contains(entry.Entity));
        }

        var entries = await query
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .ToListAsync(ct);
        return [.. entries.Select(Read)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataHistoryCount>> CountAboutAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        var ids = accountIds.ToHashSet();
        var idList = ids.ToArray();
        var entries = await context
            .DataHistory.Where(entry => entry.SubjectIds.Any(subject => idList.Contains(subject)))
            .Select(entry => new
            {
                entry.SubjectIds,
                entry.Entity,
                entry.OccurredAt,
            })
            .ToListAsync(ct);
        return
        [
            .. entries
                .SelectMany(entry =>
                    entry
                        .SubjectIds.Where(ids.Contains)
                        .Distinct()
                        .Select(accountId => (AccountId: accountId, entry.Entity, entry.OccurredAt))
                )
                .GroupBy(entry => (entry.AccountId, entry.Entity))
                .Select(group => new DataHistoryCount(
                    group.Key.AccountId,
                    group.Key.Entity,
                    group.Count(),
                    group.Min(entry => entry.OccurredAt),
                    group.Max(entry => entry.OccurredAt)
                )),
        ];
    }

    private DataHistoryItem Read(DataHistoryEntry entry)
    {
        try
        {
            var record = protector.Unprotect(entry.Id, entry.Payload);
            return new DataHistoryItem(
                entry.Id,
                entry.Entity,
                entry.OccurredAt,
                entry.SubjectIds,
                new DataHistoryContent(
                    record.Operation.ToString(),
                    record.ActorId,
                    record.Command,
                    record.Values.ToDictionary(
                        value => value.Key,
                        value =>
                            value.Value is JsonElement element
                                ? element
                                : JsonSerializer.SerializeToElement(value.Value),
                        StringComparer.Ordinal
                    )
                )
            );
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            logger.DataHistoryEntryUnreadable(ex);
            return new DataHistoryItem(
                entry.Id,
                entry.Entity,
                entry.OccurredAt,
                entry.SubjectIds,
                null
            );
        }
    }
}
