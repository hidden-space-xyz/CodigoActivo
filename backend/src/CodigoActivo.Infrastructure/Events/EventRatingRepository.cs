using CodigoActivo.Domain.Events;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;

namespace CodigoActivo.Infrastructure.Events;

/// <summary>
/// Stores event ratings.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class EventRatingRepository(CodigoActivoDbContext context)
    : AggregateRepository<EventRating>(context),
        IEventRatingRepository;
