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
    public async Task<LegalPolicy?> LockAsync(LegalPolicyKind kind, CancellationToken ct = default)
    {
        var storedKind = kind.ToString();
        FormattableString sql = $"""
            SELECT id AS "Value" FROM legal_policies WHERE kind = {storedKind} FOR UPDATE
            """;
        var ids = await Context.Database.SqlQuery<Guid>(sql).ToListAsync(ct);
        if (ids.Count is 0)
        {
            return null;
        }

        var id = LegalPolicyId.From(ids[0]);
        return await Set.SingleAsync(policy => policy.Id == id, ct);
    }
}
