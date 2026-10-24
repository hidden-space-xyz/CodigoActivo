using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Events;

/// <summary>
/// Stores event ratings.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class EventRatingRepository(CodigoActivoDbContext context)
    : AggregateRepository<EventRating>(context),
        IEventRatingRepository
{
    /// <inheritdoc />
    public Task<EventRating?> GetByAuthorAsync(
        EventId eventId,
        UserId userId,
        CancellationToken ct = default
    )
    {
        return Set.SingleOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId, ct);
    }
}
