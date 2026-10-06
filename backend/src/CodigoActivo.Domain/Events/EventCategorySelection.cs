using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Categories chosen to tag an event: at least one, each counted once.
/// </summary>
public sealed class EventCategorySelection
{
    private EventCategorySelection(IReadOnlyList<EventCategoryTypeId> categoryTypeIds)
    {
        CategoryTypeIds = categoryTypeIds;
    }

    /// <summary>
    /// Gets the distinct identifiers of the chosen category types.
    /// </summary>
    public IReadOnlyList<EventCategoryTypeId> CategoryTypeIds { get; }

    /// <summary>
    /// Checks the chosen categories.
    /// </summary>
    /// <param name="categoryTypeIds">Identifiers as supplied; repeated ones count once.</param>
    /// <returns>The selection, or an error when no category was chosen.</returns>
    public static Result<EventCategorySelection> Create(
        IReadOnlyList<EventCategoryTypeId>? categoryTypeIds
    )
    {
        return categoryTypeIds is null || categoryTypeIds.Count is 0
            ? Error.Validation(DomainErrorCode.EventCategoriesRequired)
            : new EventCategorySelection([.. categoryTypeIds.Distinct()]);
    }
}
