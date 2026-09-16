using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events;

/// <summary>
/// Checks event category references against domain rules.
/// </summary>
/// <param name="categoryTypes">Repository used to persist and retrieve category types.</param>
public sealed class EventCategoryChecker(IEventCategoryTypeRepository categoryTypes)
{
    /// <summary>
    /// Ensures that categories satisfies the required business rules.
    /// </summary>
    /// <param name="categoryTypeIds">Identifiers of the category type items.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> EnsureCategoriesAsync(
        IReadOnlyList<Guid>? categoryTypeIds,
        CancellationToken ct = default
    )
    {
        if (categoryTypeIds is null || categoryTypeIds.Count is 0)
        {
            return Error.BadRequest(ErrorCode.EventCategoriesRequired);
        }

        var distinct = categoryTypeIds.Distinct().ToList();
        var existing = await categoryTypes.CountAsync(c => distinct.Contains(c.Id), ct);
        return existing != distinct.Count
            ? (Result)Error.BadRequest(ErrorCode.EventCategoryTypeNotFound)
            : Result.Success();
    }
}
