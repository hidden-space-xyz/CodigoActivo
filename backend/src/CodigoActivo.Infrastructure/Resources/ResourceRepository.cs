using CodigoActivo.Domain.Resources;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Resources;

/// <summary>
/// Stores and loads resources.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class ResourceRepository(CodigoActivoDbContext context)
    : AggregateRepository<Resource>(context),
        IResourceRepository
{
    /// <inheritdoc />
    public Task<Resource?> GetByIdAsync(ResourceId id, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(resource => resource.Id == id, ct);
    }
}
