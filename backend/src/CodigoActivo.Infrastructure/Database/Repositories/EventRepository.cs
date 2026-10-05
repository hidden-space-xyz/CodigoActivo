using CodigoActivo.Domain.Events;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Stores and loads events with their categories and terms documents.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class EventRepository(CodigoActivoDbContext context)
    : AggregateRepository<Event>(context),
        IEventRepository
{
    /// <inheritdoc />
    public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default)
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
    public Task<bool> LinksTermsDocumentAsync(Guid termsDocumentId, CancellationToken ct = default)
    {
        return Set.AnyAsync(
            e => e.TermsDocuments.Any(link => link.TermsDocumentId == termsDocumentId),
            ct
        );
    }

    /// <inheritdoc />
    public Task<bool> HasEventWithOnlyCategoryAsync(
        Guid categoryTypeId,
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
