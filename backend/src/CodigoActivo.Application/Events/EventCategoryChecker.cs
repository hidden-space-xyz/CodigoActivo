using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events;

/// <summary>
/// Checks the categories chosen for an event: the event requires at least one, and each must be
/// a stored category type.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
public sealed class EventCategoryChecker(IEventCategoryTypeRepository categoryTypes)
{
    /// <summary>
    /// Checks the chosen categories.
    /// </summary>
    /// <param name="categoryTypeIds">Identifiers of the category type items.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the selection, or the error of the first broken rule.</returns>
    public async Task<Result<EventCategorySelection>> EnsureCategoriesAsync(
        IReadOnlyList<EventCategoryTypeId> categoryTypeIds,
        CancellationToken ct = default
    )
    {
        var selection = EventCategorySelection.Create(categoryTypeIds);
        if (selection.IsFailure)
        {
            return selection;
        }

        var ids = selection.Value.CategoryTypeIds;
        return await categoryTypes.CountExistingAsync(ids, ct) != ids.Count
            ? Error.Validation(ApplicationErrorCode.EventCategoryTypeNotFound)
            : selection;
    }
}
