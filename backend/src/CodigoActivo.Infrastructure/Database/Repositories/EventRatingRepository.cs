using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;

namespace CodigoActivo.Infrastructure.Database.Repositories;

public class EventRatingRepository(CodigoActivoDbContext context)
    : Repository<EventRating>(context),
        IEventRatingRepository;
