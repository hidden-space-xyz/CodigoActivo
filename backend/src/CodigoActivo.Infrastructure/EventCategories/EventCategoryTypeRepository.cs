using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.EventCategories;

/// <summary>
/// Stores and loads event categories.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class EventCategoryTypeRepository(CodigoActivoDbContext context)
    : AggregateRepository<EventCategoryType>(context),
        IEventCategoryTypeRepository
{
    /// <inheritdoc />
    public Task<EventCategoryType?> GetByIdAsync(
        EventCategoryTypeId id,
        CancellationToken ct = default
    )
    {
        return Set.FirstOrDefaultAsync(categoryType => categoryType.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<bool> LockAsync(
        EventCategoryType categoryType,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(categoryType);

        FormattableString sql = $"""
            SELECT id AS "Value" FROM event_category_types WHERE id = {categoryType.Id.Value} FOR UPDATE
            """;
        return (await Context.Database.SqlQuery<Guid>(sql).ToListAsync(ct)).Count is not 0;
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(
        string name,
        EventCategoryTypeId? exceptId = null,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            categoryType =>
                EF.Functions.ILike(
                    categoryType.Name,
                    LikePattern.Literal(name),
                    LikePattern.EscapeCharacter
                ) && (exceptId == null || categoryType.Id != exceptId.Value),
            ct
        );
    }

    /// <inheritdoc />
    public Task<int> CountExistingAsync(
        IReadOnlyCollection<EventCategoryTypeId> ids,
        CancellationToken ct = default
    )
    {
        return Set.CountAsync(categoryType => ids.Contains(categoryType.Id), ct);
    }
}
