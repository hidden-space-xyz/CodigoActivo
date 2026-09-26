using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves the disposable email domain list from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public sealed class DisposableEmailDomainRepository(CodigoActivoDbContext context)
    : IDisposableEmailDomainRepository
{
    /// <inheritdoc />
    public Task<bool> ContainsAnyAsync(
        IReadOnlyCollection<string> domains,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(domains);

        return context
            .DisposableEmailDomains.AsNoTracking()
            .AnyAsync(entry => domains.Contains(entry.Domain), ct);
    }

    /// <inheritdoc />
    public async Task ReplaceAsync(IReadOnlySet<string> domains, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domains);

        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var stored = await context
            .DisposableEmailDomains.Select(entry => entry.Domain)
            .ToListAsync(ct);
        var removed = stored.Where(domain => !domains.Contains(domain)).ToList();
        var added = domains.Except(stored, StringComparer.Ordinal).ToList();

        if (removed.Count > 0)
        {
            await context
                .DisposableEmailDomains.Where(entry => removed.Contains(entry.Domain))
                .ExecuteDeleteAsync(ct);
        }

        if (added.Count > 0)
        {
            await context.DisposableEmailDomains.AddRangeAsync(
                added.Select(domain => new DisposableEmailDomain { Domain = domain }),
                ct
            );
            await context.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
