using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Infrastructure.Database.Configurations;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.TermsDocuments;

/// <summary>
/// Stores and loads terms documents.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class TermsDocumentRepository(CodigoActivoDbContext context)
    : AggregateRepository<TermsDocument>(context),
        ITermsDocumentRepository
{
    /// <inheritdoc />
    public Task<TermsDocument?> GetByIdAsync(TermsDocumentId id, CancellationToken ct = default)
    {
        return Set.Include(document => document.PreviousVersions)
            .FirstOrDefaultAsync(document => document.Id == id, ct);
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(
        string name,
        TermsDocumentId? exceptId = null,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            document =>
                EF.Functions.Collate(document.Name, CaseInsensitiveNames.Collation) == name
                && (exceptId == null || document.Id != exceptId.Value),
            ct
        );
    }

    /// <inheritdoc />
    public Task<int> CountExistingAsync(
        IReadOnlyCollection<TermsDocumentId> ids,
        CancellationToken ct = default
    )
    {
        return Set.CountAsync(document => ids.Contains(document.Id), ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TermsDocumentId>> ListRequiringSignatureAsync(
        IReadOnlyCollection<TermsDocumentId> ids,
        CancellationToken ct = default
    )
    {
        return await Set.Where(document => document.RequiresSignature && ids.Contains(document.Id))
            .Select(document => document.Id)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TermsDocumentId>> ListConsentsAsync(
        IReadOnlyCollection<TermsDocumentId> ids,
        CancellationToken ct = default
    )
    {
        return await Set.Where(document =>
                document.Kind == TermsDocumentKind.Consent && ids.Contains(document.Id)
            )
            .Select(document => document.Id)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<TermsDocumentId, int>> ListVersionsAsync(
        IReadOnlyCollection<TermsDocumentId> ids,
        CancellationToken ct = default
    )
    {
        return await Set.Where(document => ids.Contains(document.Id))
            .Select(document => new { document.Id, document.Version })
            .ToDictionaryAsync(document => document.Id, document => document.Version, ct);
    }
}
