using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events;

public sealed class EventCategoryChecker(IEventCategoryTypeRepository categoryTypes)
{
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
