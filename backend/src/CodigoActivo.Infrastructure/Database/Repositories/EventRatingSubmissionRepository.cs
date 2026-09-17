using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves event rating submission data from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class EventRatingSubmissionRepository(CodigoActivoDbContext context)
    : IEventRatingSubmissionRepository
{
    /// <summary>
    /// Creates a query for the stored entities without tracking changes.
    /// </summary>
    /// <returns>The resulting event rating submission value.</returns>
    public IQueryable<EventRatingSubmission> Query()
    {
        return context.EventRatingSubmissions.AsNoTracking();
    }
}
