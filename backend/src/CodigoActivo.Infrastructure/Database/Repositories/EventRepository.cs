using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves event data from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class EventRepository(CodigoActivoDbContext context)
    : Repository<Event>(context),
        IEventRepository
{
    /// <summary>
    /// Gets the event with the relationships required for editing.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching event, or <see langword="null"/> when it is not found.</returns>
    public async Task<Event?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        return await Set.Include(e => e.Categories)
            .Include(e => e.TermsDocuments)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    /// <summary>
    /// Sets the featured state.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default)
    {
        return SetExclusiveFeaturedAsync(Set, id, ct);
    }

    /// <summary>
    /// Gets the terms acceptances recorded by a user for an event, tracked by the change tracker
    /// so callers can update an existing decision in place.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching event terms acceptance items.</returns>
    public async Task<IReadOnlyList<EventTermsAcceptance>> ListTermsAcceptancesAsync(
        Guid eventId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await Context
            .EventTermsAcceptances.Where(x => x.EventId == eventId && x.UserId == userId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Determines whether a terms document is currently linked to any event.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> HasTermsDocumentAsync(Guid termsDocumentId, CancellationToken ct = default)
    {
        return Context.EventTermsDocuments.AnyAsync(x => x.TermsDocumentId == termsDocumentId, ct);
    }

    /// <summary>
    /// Determines whether terms acceptances exists.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> HasTermsAcceptancesAsync(Guid termsDocumentId, CancellationToken ct = default)
    {
        return Context.EventTermsAcceptances.AnyAsync(
            x => x.TermsDocumentId == termsDocumentId,
            ct
        );
    }

    /// <summary>
    /// Adds a terms acceptance to the current unit of work.
    /// </summary>
    /// <param name="acceptance">The acceptance value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddTermsAcceptanceAsync(
        EventTermsAcceptance acceptance,
        CancellationToken ct = default
    )
    {
        await Context.EventTermsAcceptances.AddAsync(acceptance, ct);
    }

    /// <summary>
    /// Creates a query for the terms documents linked to events, without tracking changes.
    /// </summary>
    /// <returns>The resulting event terms document value.</returns>
    public IQueryable<EventTermsDocument> QueryTermsDocuments()
    {
        return Context.EventTermsDocuments.AsNoTracking();
    }

    /// <summary>
    /// Creates a query for the recorded terms acceptances, without tracking changes. Queries must
    /// use this method instead of <see cref="ListTermsAcceptancesAsync"/>, which stays tracked
    /// exclusively for <c>TermsGate</c> to update an existing decision in place.
    /// </summary>
    /// <returns>The resulting event terms acceptance value.</returns>
    public IQueryable<EventTermsAcceptance> QueryTermsAcceptances()
    {
        return Context.EventTermsAcceptances.AsNoTracking();
    }
}
