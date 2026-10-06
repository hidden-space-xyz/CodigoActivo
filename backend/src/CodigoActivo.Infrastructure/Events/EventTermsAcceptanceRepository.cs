using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Events;

/// <summary>
/// Stores and loads the terms decisions people take about events.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class EventTermsAcceptanceRepository(CodigoActivoDbContext context)
    : AggregateRepository<EventTermsAcceptance>(context),
        IEventTermsAcceptanceRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<EventTermsAcceptance>> ListAsync(
        EventId eventId,
        UserId userId,
        CancellationToken ct = default
    )
    {
        return await Set.Where(acceptance =>
                acceptance.EventId == eventId && acceptance.UserId == userId
            )
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> AnyForDocumentAsync(
        TermsDocumentId termsDocumentId,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(acceptance => acceptance.TermsDocumentId == termsDocumentId, ct);
    }
}
