using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Events;

/// <summary>
/// Stores and loads events with their categories and terms documents.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class EventRepository(CodigoActivoDbContext context)
    : AggregateRepository<Event>(context),
        IEventRepository
{
    /// <inheritdoc />
    public Task<Event?> GetByIdAsync(EventId id, CancellationToken ct = default)
    {
        return Set.Include(e => e.Categories)
            .Include(e => e.TermsDocuments)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Event>> ListFeaturedAsync(CancellationToken ct = default)
    {
        return await Set.Where(e => e.Featured).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> LinksTermsDocumentAsync(
        TermsDocumentId termsDocumentId,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            e => e.TermsDocuments.Any(link => link.TermsDocumentId == termsDocumentId),
            ct
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Event>> LockTaggedWithAsync(
        EventCategoryTypeId categoryTypeId,
        CancellationToken ct = default
    )
    {
        FormattableString sql = $"""
            SELECT e.id AS "Value"
            FROM events e
            WHERE EXISTS (
                SELECT 1 FROM event_categories c
                WHERE c.event_id = e.id AND c.event_category_type_id = {categoryTypeId.Value}
            )
            ORDER BY e.id
            FOR NO KEY UPDATE OF e
            """;
        var ids = (await Context.Database.SqlQuery<Guid>(sql).ToListAsync(ct))
            .Select(EventId.From)
            .ToList();
        return ids.Count is 0
            ? []
            : await Set.Include(e => e.Categories)
                .Include(e => e.TermsDocuments)
                .Where(e => ids.Contains(e.Id))
                .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Event?> GetLockedAsync(EventId id, CancellationToken ct = default)
    {
        FormattableString sql = $"""
            SELECT id AS "Value" FROM events WHERE id = {id.Value} FOR NO KEY UPDATE
            """;
        return (await Context.Database.SqlQuery<Guid>(sql).ToListAsync(ct)).Count is 0
            ? null
            : await GetByIdAsync(id, ct);
    }
}
