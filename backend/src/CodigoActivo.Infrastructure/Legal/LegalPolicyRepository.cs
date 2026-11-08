using CodigoActivo.Domain.Legal;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Legal;

/// <summary>
/// Stores and loads the legal policies.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class LegalPolicyRepository(CodigoActivoDbContext context)
    : AggregateRepository<LegalPolicy>(context),
        ILegalPolicyRepository
{
    /// <inheritdoc />
    public Task<LegalPolicy?> GetByKindAsync(LegalPolicyKind kind, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(policy => policy.Kind == kind, ct);
    }
}
