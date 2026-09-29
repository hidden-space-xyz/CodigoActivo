using CodigoActivo.Domain.Partners;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Stores and loads partners.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class PartnerRepository(CodigoActivoDbContext context)
    : AggregateRepository<Partner>(context),
        IPartnerRepository
{
    /// <inheritdoc />
    public Task<Partner?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(partner => partner.Id == id, ct);
    }
}
