using CodigoActivo.Domain.Inventory;
using CodigoActivo.Infrastructure.Database.Configurations;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Inventory;

/// <summary>
/// Stores and loads inventory categories with their columns, options and entries.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class InventoryCategoryRepository(CodigoActivoDbContext context)
    : AggregateRepository<InventoryCategory>(context),
        IInventoryCategoryRepository
{
    /// <inheritdoc />
    public Task<InventoryCategory?> GetByIdAsync(
        InventoryCategoryId id,
        CancellationToken ct = default
    )
    {
        return Set.Include(category => category.Columns)
                .ThenInclude(column => column.Options)
            .Include(category => category.Entries)
            .AsSplitQuery()
            .FirstOrDefaultAsync(category => category.Id == id, ct);
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(
        string name,
        InventoryCategoryId? exceptId = null,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            category =>
                EF.Functions.Collate(category.Name, CaseInsensitiveNames.Collation) == name
                && (exceptId == null || category.Id != exceptId.Value),
            ct
        );
    }
}
