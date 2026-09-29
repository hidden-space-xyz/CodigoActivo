using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.EventCategories.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.EventCategories.Queries;

/// <summary>
/// Carries the criteria used to retrieve an event category type by identifier.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the event category type.</param>
public sealed record GetEventCategoryTypeByIdQuery(Guid CategoryTypeId)
    : IQuery<Result<EventCategoryTypeResponse>>;

/// <summary>
/// Executes the query to retrieve an event category type by identifier.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventCategoryTypeByIdQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<GetEventCategoryTypeByIdQuery, Result<EventCategoryTypeResponse>>
{
    /// <summary>
    /// Handles the request to retrieve an event category type by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the category type, or an application error when it does not exist.</returns>
    public async Task<Result<EventCategoryTypeResponse>> HandleAsync(
        GetEventCategoryTypeByIdQuery query,
        CancellationToken ct = default
    )
    {
        var response = await executor.FirstOrDefaultAsync(
            readStore
                .EventCategoryTypes.Where(categoryType => categoryType.Id == query.CategoryTypeId)
                .Select(EventCategoryProjections.EventCategoryType),
            ct
        );
        return response is null ? Error.NotFound(ErrorCode.EventCategoryTypeNotFound) : response;
    }
}
