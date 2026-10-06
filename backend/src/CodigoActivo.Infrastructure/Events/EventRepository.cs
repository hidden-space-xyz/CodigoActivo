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
    public Task<bool> HasEventWithOnlyCategoryAsync(
        EventCategoryTypeId categoryTypeId,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            e =>
                e.Categories.Count == 1
                && e.Categories.Any(category => category.EventCategoryTypeId == categoryTypeId),
            ct
        );
    }
}
