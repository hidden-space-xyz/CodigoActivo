using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Stores and loads terms documents.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class TermsDocumentRepository(CodigoActivoDbContext context)
    : AggregateRepository<TermsDocument>(context),
        ITermsDocumentRepository
{
    /// <inheritdoc />
    public Task<TermsDocument?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(document => document.Id == id, ct);
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            document =>
                EF.Functions.ILike(
                    document.Name,
                    LikePattern.Literal(name),
                    LikePattern.EscapeCharacter
                ) && (exceptId == null || document.Id != exceptId.Value),
            ct
        );
    }

    /// <inheritdoc />
    public Task<int> CountExistingAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    )
    {
        return Set.CountAsync(document => ids.Contains(document.Id), ct);
    }
}
